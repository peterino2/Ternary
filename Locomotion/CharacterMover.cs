using Godot;
using System;

public partial class CharacterMover: Node
{
	public long OwnerId = 0;
	[Export] public CollisionShape3D Collision;
	[Export] public float MovementSpeed = 2.5f;

	// === settings ===
	[Export] public float ServerMovementLeniency = 0.3f;
	[Export] public float MaxPredictionDesyncLength = 0.8f;
	[Export] public float MinPredictionDesyncLength = 0.3f;
	[Export] public float PredictionDesyncLerpScale = 0.1f;
	
	[Export] public Node3D Base;
	[Export] public bool UsePredictionSmoothing = true;

	[Export] public Vector3 PositionSync = new Vector3(0,0,0);
	[Export] public Vector2 NetMoveSync = new Vector2(0,0);

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
	private double NetTickTime  = 0;

	float SphereRadius = 0.0f;

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
		Rpc(nameof(RecieveGameStateClient), new Variant[] {CommandFrame, PositionSync, NetMoveSync});
	}

	// Called when it's time to submit the current state
	public void OnNetTick(long CommandFrame, double Delta)
	{
		RpcId(1, nameof(SubmitMove), new Variant[] {
			GameNetEngine.Get().CommandFrame, 
			AccumulatedMovement,
			Base.Position
		});
		AccumulatedMovement = new Vector2(0,0);
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
		DebugDraw3D.DrawSphere(ServerPredictedPosition, 0.6f, Colors.Yellow, 0.1f);
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
		DebugDraw3D.DrawSphere(Base.Position, 0.6f, Colors.Green, 0.1f);
	}

	private void TickInterpolation(double delta)
	{
		if((PositionSync - Base.Position).Length() < (float) delta * MovementSpeed)
		{
			Base.Position = PositionSync;
		}
		else {
			Base.Position = Base.Position + (PositionSync - Base.Position).Normalized() * (float) delta * MovementSpeed;
		}
		DebugDraw3D.DrawSphere(PositionSync, 0.6f, Colors.Red, 0.1f);
	}

	// This function is called on all clients when the server broadcasts the
	// state of this class for a given command Frame
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(long CommandFrame, Vector3 NewPositionSync, Vector2 NewNetMoveSync)
	{
		PositionSync = NewPositionSync;
		NetMoveSync = NewNetMoveSync;
		LastDelta = GameNetEngine.Get().TickDelta;
		AccumulatedMovementSinceSync = new Vector2(0, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SubmitMove(long CommandFrame, Vector2 NewMove, Vector3 ProposedPosition)
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
		Base.Position = SimulateMovedPosition(Base.Position, AccumulatedMovement, MovementSpeed);

		if((ProposedPosition -  Base.Position).Length() < 0.3f * (float) GameNetEngine.Get().TickDelta)
		{
			Base.Position = ProposedPosition;
		}
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
		AddMovementInputRaw(MovementInput.Normalized() * (float) delta);
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
			MovementInput.X * SimulatedMovementSpeed,
			0,
			MovementInput.Y * SimulatedMovementSpeed
		);

		var MovedPosition = StartPosition + DeltaPosition;

		var motionParameters = new PhysicsTestMotionParameters3D();
		motionParameters.From = new Transform3D(new Basis(1,0,0,0,1,0,0,0,1), StartPosition);;
		motionParameters.Motion = DeltaPosition;

		if(BaseIsCharacter)
		{
			if(DeltaPosition.Length() > 0.001)
			{
				PhysicsTestMotionResult3D Results = new PhysicsTestMotionResult3D();
				PhysicsServer3D.BodyTestMotion(BaseAsCharacterBody.GetRid(), motionParameters, Results);
				NU.Ok(Results.GetTravel().ToString());
                MovedPosition = StartPosition + Results.GetTravel();
			}
		}

		// TODO: do collision resolution here.
		// Logic for resolving collisions, 
		
		// MovedPosition is our proposed new Position, 
		// we need to run a collision sweep from our current

		// var SpaceState = Base.GetWorld3D().DirectSpaceState;
		// var Parameters = new PhysicsShapeQueryParameters3D();
		// Parameters.ShapeRid = ShapeRid;
		// Parameters.Transform.origin = StartPosition;
		// Parameters.Motion = DeltaPosition;

		// if(BaseIsCollision)
		// {
		// 	Parameters.Exclude.Add(BaseAsCollision.GetRid());
		// }

		// var Result = SpaceState.IntersectShape(Parameters);

		// if(first)
		// {
		// 	first = false;
		// 	NU.Ok(Result.ToString());
		// }

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
}
