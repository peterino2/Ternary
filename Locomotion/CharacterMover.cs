using Godot;
using System;

public partial class CharacterMover: Node
{
	public long OwnerId = 0;
	[Export] public CollisionShape3D Collision;
	[Export] public float MovementSpeed = 2.5f; // Max movement speed
	[Export] public float Accelleration = 5.5f; // Accelleration
	[Export] public float Decelleration = 5.5f; // Accelleration
	[Export] public float MinSpeed   = 2.0f ; // minimum velocity

	// === settings ===
	[Export] public float ServerMovementLeniency = 0.3f;
	[Export] public float MaxPredictionDesyncLength = 0.8f;
	[Export] public float MinPredictionDesyncLength = 0.3f;
	[Export] public float PredictionDesyncLerpScale = 0.1f;
	
	[Export] public Node3D Base;
	[Export] public bool UsePredictionSmoothing = true;
	[Export] public bool UseInterpolation = true;

	[Export] public bool DebugMovement = false;
	[Export] public bool DebugCollisionResolve = false;

	public Vector3 PositionSync = new Vector3(0,0,0);
	public Vector2 Velocity = new Vector2(0,0);
	public Vector2 NetMoveSync = new Vector2(0,0);

    public Vector2 DodgeVelocityOverride = new Vector2(0,0);
    public double DodgeVelocityOverrideDuration = 0.0;

    public bool Ghosting = false;

	float InterpolationSpeed = 5.0f;

	public CollisionObject3D BaseAsCollision;
	public bool BaseIsCollision = false;
	
	public CharacterBody3D BaseAsCharacterBody;
	public bool BaseIsCharacter = false;

	SphereShape3D SphereShape;
	bool ShapeRidReady = false;
	Rid ShapeRid;

	public double LastDelta = 0.01f;
	// Input Vector scaled for this last frame
	Vector2 LocalInput = new Vector2(0,0);

	// Accumulated sum of inputs since the last move submission
	Vector2 AccumulatedMovement = new Vector2(0,0);

	// Accumulated sum of inputs since the last command frame state
	Vector2 AccumulatedMovementSinceSync = new Vector2(0,0);

	public bool IsLocallyControlled = false;


	float SphereRadius = 0.0f;

	int SyncFrameCount = 2;

	public override void _Ready() 
	{
	}

	// call me from your object's ready after my setup is complete
	public void SetupNetTickables()
	{
		if(OwnerId == GameSession.Get().PeerId)
		{
			NU.Ok("Movement component setup to send.");
			GameNetEngine.Get().OnNetTick += OnNetTick;
			IsLocallyControlled = true;
		}

		if(GameSession.Get().IsServer())
		{
			NU.Ok("Movement component setup, reciever");
			GameNetEngine.Get().OnSyncFrame += OnSyncFrame;
		}
	}

	public void SetBase(Node3D NewBase)
	{
		Base = NewBase;
		PositionSync = Base.Position;

		SphereShape = (SphereShape3D) Collision.Shape;
		if(SphereShape != null)
		{
			ShapeRid = PhysicsServer3D.SphereShapeCreate();
			PhysicsServer3D.ShapeSetData(ShapeRid, SphereRadius);
			ShapeRidReady = true;
		}

		BaseAsCollision = (CollisionObject3D) Base;
		if(BaseAsCollision != null)
		{
			BaseIsCollision = true;
		}

		BaseAsCharacterBody = (CharacterBody3D) Base;
		if(BaseAsCharacterBody != null)
		{
			BaseIsCharacter = true;
		}
		else 
		{
			NU.Error("Character is NOT a CharacterBody3D?");
		}
	}

	// Called when it's time to broadcast the current state
	public void OnSyncFrame(long CommandFrame, double Delta)
	{
		if(SyncFrameCount > 0)
		{
			Rpc(nameof(RecieveGameStateClient), new Variant[] {GameNetEngine.Get().GetTime(), PositionSync, NetMoveSync});
			SyncFrameCount -= 1;
		}
	}

	// Called when it's time to submit the current state
	public void OnNetTick(long CommandFrame, double Delta)
	{
		if(AccumulatedMovement.Length() > 0.01)
		{
			RpcId(1, nameof(SubmitMove), new Variant[] {
				GameNetEngine.Get().GetTime(), 
				AccumulatedMovement,
				Base.Position
			});
			AccumulatedMovement = new Vector2(0,0);
		}
	}

	public void SetOwner(long NewOwnerId) 
	{
		OwnerId = NewOwnerId;
		IsLocallyControlled = (OwnerId == GameSession.Get().PeerId);
	}

	// Have this get called from the owner's _Process function, at the end.
	// do it after all AddMovementInput()s are called
	// updates every frame
	public void TickUpdates(double Delta)
	{
		//1. Server Auth move.
		if(IsLocallyControlled)
		{
			TickPrediction(Delta);
		}
		else 
		{
			TickInterpolation(Delta);
		}
	}

	public Vector2 GetLastMove()
	{
		if(!IsLocallyControlled)
		{
			return NetMoveSync;
		}
		else
		{
			return LocalInput;
		}
	}

	private void TickPrediction(double delta)
	{
		var ServerPredictedPosition = SimulateMovedPosition(PositionSync, AccumulatedMovementSinceSync, MovementSpeed);
		
		if(DebugMovement)
			DebugDraw3D.DrawSphere(ServerPredictedPosition + new Vector3(0,0.6f,0), 0.6f, Colors.Yellow, 0.1f);
		if(UsePredictionSmoothing)
		{
			Base.Position = SimulateMovedPosition(Base.Position, LocalInput, MovementSpeed);
			Vector3 NetError = Base.Position - (ServerPredictedPosition);
			if(NetError.Length() > MinPredictionDesyncLength)
			{
				Base.Position = Base.Position.Lerp(ServerPredictedPosition, PredictionDesyncLerpScale);
			}
			else if(NetError.Length() > MaxPredictionDesyncLength)
			{
				Base.Position = ServerPredictedPosition;
			}
		}
		else 
		{
			Base.Position = ServerPredictedPosition;
		}

        DodgeVelocityOverrideDuration -= delta;

		if(DebugMovement)
        {
			DebugDraw3D.DrawSphere(Base.Position + new Vector3(0, 0.6f, 0), 0.6f, Colors.Green, 0.1f);
            if(DodgeVelocityOverrideDuration > 0)
            {
			    DebugDraw3D.DrawSphere(Base.Position + new Vector3(0, 0.6f, 0), 0.6f, Colors.Cyan, 0.1f);
            }
        }

	}

	private void TickInterpolation(double delta)
	{
		if(UseInterpolation)
		{
			if((PositionSync - Base.Position).Length() < (float) delta * InterpolationSpeed)
			{
				Base.Position = PositionSync;
			}
			else {
				Base.Position = Base.Position + (PositionSync - Base.Position).Normalized() * (float) delta * InterpolationSpeed;
			}
		}
		else 
		{
			Base.Position = PositionSync;
		}

		if(DebugMovement)
			DebugDraw3D.DrawSphere(PositionSync + new Vector3(0,0.6f,0), 0.6f, Colors.Blue, 0.1f);
	}

	// This function is called on all clients when the server broadcasts the
	// state of this class for a given command Frame
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(double StateTime, Vector3 NewPositionSync, Vector2 NewNetMoveSync)
	{
		LastDelta = GameNetEngine.Get().TickDelta;
		InterpolationSpeed = (NewPositionSync - PositionSync).Length() / ((float)LastDelta);
		NetMoveSync = NewNetMoveSync;
		AccumulatedMovementSinceSync = new Vector2(0, 0);

        if(!IsLocallyControlled)
        {
            Velocity = NetMoveSync.Normalized() * InterpolationSpeed;
            if(NetMoveSync.Length() < 0.01 && (PositionSync - Base.Position).Length() > 0.5f)
            {
                Base.Position = NewPositionSync;
            }
        }

		PositionSync = NewPositionSync;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SubmitMove(double StateTime, Vector2 NewMove, Vector3 ProposedPosition)
	{
		var Sender = Multiplayer.GetRemoteSenderId();
		if(GameSession.Get().PeerId != 1)
		{
			NU.Error(
				"This is a server only function, it's been submitted to peer " 
				+ GameSession.Get().PeerId.ToString()
				+ " by " + Sender.ToString()
			);
			return;
		}

		// Check that the sender has the ownership needed to move us
		if(Sender != OwnerId)
		{
			NU.Error(
				"Client" 
				+ Sender.ToString()
				+ " is trying to send moves to me but i'm owned by " + OwnerId.ToString()
			);
			return;
		}

		AccumulatedMovement = NewMove;
		LastDelta = GameNetEngine.Get().TickDelta;
		Base.Position = SimulateMovedPosition(Base.Position, AccumulatedMovement, AccumulatedMovement.Length() / (float) LastDelta);


		if((ProposedPosition -  Base.Position).Length() < 0.3f * (float) GameNetEngine.Get().TickDelta)
		{
			// Base.Position = ProposedPosition;
			DebugDraw3D.DrawSphere(ProposedPosition + new Vector3(0,0.6f,0), 0.6f, Colors.Orange, 0.1f);
		}
		else 
		{
			DebugDraw3D.DrawSphere(ProposedPosition + new Vector3(0,0.6f,0), 0.6f, Colors.Red, 0.1f);
		}

		SyncFrameCount = Math.Max(2, SyncFrameCount);
		NetMoveSync = AccumulatedMovement;
		PositionSync = Base.Position;
	}

	// Adds basic movement input, use axis values and delta time.
	// you typically want to use this one.
	public void AddMovementInput(Vector2 MovementInput, double delta)
	{
		if(!IsLocallyControlled)
		{
			NU.Error("Client is adding Movement input to a character it doesn't own : " + OwnerId.ToString());
			return;
		}

		if(MovementInput.Length() > 0.3)
		{
			if(Velocity.Length() < MinSpeed)
			{
				Velocity = MinSpeed * MovementInput.Normalized();
			}
		}
		else
		{
			Velocity -= Velocity.Normalized() * Decelleration * (float) delta;

			if(Velocity.Length() < 0.05f)
			{
				Velocity = new Vector2(0, 0);
			}
		}

		Velocity += MovementInput.Normalized() * (float) delta * (float) Accelleration;

		var HorizontalComponent = (Velocity) - MovementInput.Normalized().Dot(Velocity) * MovementInput.Normalized();
		Velocity -= HorizontalComponent * 0.5f * (float) delta;

		if(MovementInput.Length() > 0.3)
		{
            if(Velocity.Length() > MovementSpeed)
            {
                Velocity = Velocity.Normalized() * MovementSpeed;
            }
        }

        if(DodgeVelocityOverrideDuration > 0)
        {
            Velocity = DodgeVelocityOverride; 
        }

		//AddMovementInputRaw(MovementInput.Normalized() * (float) delta);
		AddMovementInputRaw(Velocity * (float) delta);
		LastDelta = delta;

	}

	// Low level adds an accumulated value to the movement input
	public void AddMovementInputRaw(Vector2 MovementInput)
	{
		LocalInput = MovementInput;
		AccumulatedMovement += MovementInput;
		AccumulatedMovementSinceSync += MovementInput;
	}

	bool first = true;


	// Simulated movement logic
	public virtual Vector3 SimulateMovedPosition(
			Vector3 StartPosition,
			Vector2 MovementInput,
			float SimulatedMovementSpeed)
	{
		var DeltaPosition = new Vector3(
			MovementInput.X,
			0,
			MovementInput.Y
		);

		var MovedPosition = StartPosition + DeltaPosition;


		if(BaseIsCharacter)
		{
			if(DeltaPosition.Length() > 0.001 && !Ghosting)
			{
                var motionParameters = new PhysicsTestMotionParameters3D();
                motionParameters.From = new Transform3D(new Basis(1,0,0,0,1,0,0,0,1), StartPosition);
                motionParameters.Motion = DeltaPosition;

				PhysicsTestMotionResult3D Results = new PhysicsTestMotionResult3D();
				PhysicsServer3D.BodyTestMotion(BaseAsCharacterBody.GetRid(), motionParameters, Results);

				var Travel = Results.GetTravel();
				Travel.Y = 0;
				MovedPosition = StartPosition + Travel;
				if(Results.GetCollisionCount() > 0)
				{

					if (Math.Abs((Travel.Length() - DeltaPosition.Length())) > 0.01) 
					{
						// todo recursively apply this movement
						var LeftOverTravel = DeltaPosition - Travel;
						var Normal = Results.GetCollisionNormal();

						if(DebugCollisionResolve)
							DebugDraw3D.DrawSphere(MovedPosition + new Vector3(0,0.6f,0), 0.6f, Colors.Red, 0.1f);

						// subtract the Normal component from our movement vector and slide along that direction.
						LeftOverTravel -= Normal.Dot((LeftOverTravel)) * Normal;

						MovedPosition += LeftOverTravel;
						if(DebugCollisionResolve)
							DebugDraw3D.DrawSphere(MovedPosition + new Vector3(0,0.6f,0), 0.6f, Colors.Yellow, 0.1f);
					}
					else if(Travel.Length() < 0.001)
					{
						// ehh.. why not
						MovedPosition += DeltaPosition * 0.5f;
					}
				}
			}
		}
		MovedPosition.Y = 0.0f;

		return MovedPosition;
	}

	public void ShutdownNetTickables() 
	{
		if(OwnerId == GameSession.Get().PeerId || IsLocallyControlled)
		{
			GameNetEngine.Get().OnNetTick -= OnNetTick;
		}
		if(GameSession.Get().IsServer())
		{
			GameNetEngine.Get().OnSyncFrame -= OnSyncFrame;
		}
	}

    public void OverridePosition(Vector3 NewPosition)
    {
		SyncFrameCount = 20;
		NetMoveSync = new Vector2(0,0);
		PositionSync = NewPosition;
        Base.Position = NewPosition;
    }

    // if ClientAuthority is set, the server shall accept the client's position as true.
    [Export] bool ClientAuthority = false;
    public void ServerSetClientAuthority(bool NewClientAuthority)
    {
        ClientAuthority =  NewClientAuthority;

        RpcId(OwnerId, nameof(BroadcastSetClientAuthority), new Variant[] {
            ClientAuthority
        });
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void BroadcastSetClientAuthority(bool NewClientAuthority)
    {
        SetClientAuthorityLocal(NewClientAuthority);
    }

    public void SetClientAuthorityLocal(bool NewClientAuthority)
    {
        ClientAuthority = NewClientAuthority;
    }
}
