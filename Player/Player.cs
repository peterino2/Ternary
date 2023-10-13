using Godot;
using Godot.Collections;
using System;

public partial class Player : CharacterBody3D
{
	AnimatedGameSprite Sprite;

	[Export] CharacterMover Mover;
	[Export] ProjectileSpawner Projectiles;

	[Export] public float WalkingSpeed = 2.5f;
	[Export] public long OwnerId = 0;
	[Export] public Node3D ArrowBase;
	[Export] public MultiplayerSynchronizer Sync;
	[Export] public PackedScene BallScene;

	[Export] public Node3D Indicator;
	[Export] public MeshInstance3D HoldingBallMesh;

	public WorldBall PickedUpBall;

	Vector2 AccumulatedMovement = new Vector2(0,0);
	Vector2 SyncAccumulatedMovement = new Vector2(0,0);
	Vector2 LocalInput = new Vector2(0,0);
	Vector3 MouseVector = new Vector3(0,0,0);
	Vector3 MouseWorldPosition;
	Vector3 LastDeltaPosition = new Vector3(0,0,0);

	static Rid PickupShapeRid;
	static bool PickupShapeReady = false;
	[Export] float PickupRadius = 0.6f;

	public bool HoldingBall = false;
	public string DebugName = "Unnamed";

	bool MouseInWindow = false;
	bool FireButtonDown = false;

	float CachedBaseMoveSpeed = 0.0f;
	float CachedMinMoveSpeed = 0.0f;
	float ChargingSlowFactor = 0.5f;

	[Export] double ChargeTimeToThrow = 1.0;
	double ChargeTime = 0.0;

	[Export] bool UsePrediction = true;

	public bool IsLocalPlayer = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Sprite = GetNode<AnimatedGameSprite>("Node3D/Sprite");
		Sprite.Play("IdleDown");

		if(!PickupShapeReady)
		{
			PickupShapeRid = PhysicsServer3D.SphereShapeCreate();
			PhysicsServer3D.ShapeSetData(PickupShapeRid, PickupRadius);
			PickupShapeReady = true;
		}

		if(OwnerId == GameSession.Get().PeerId)
		{
			RegisterAsLocalPlayer();
			IsLocalPlayer = true;
		}

		Mover.SetOwner(OwnerId);
		Mover.SetBase(this);
		Mover.SetupNetTickables();
		CachedBaseMoveSpeed = Mover.MovementSpeed;
		CachedMinMoveSpeed = Mover.MinSpeed;

		Projectiles.SetOwner(OwnerId);
		Projectiles.SetBase(this);
		Projectiles.SetupNetTickables();

		SetupNetTickables();
	}

	public void SetOwnerServer(long NewOwner)
	{
		OwnerId = NewOwner;
		Mover.SetOwner(NewOwner);
	}

	// Called by Server to Update the Ownership of a given actor
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetOwnerOnClient(long NewOwner)
	{
		OwnerId = NewOwner;
		Mover.SetOwner(NewOwner);

		if(NewOwner == GameSession.Get().PeerId)
		{
			RegisterAsLocalPlayer();
		}
	}

	public void RegisterAsLocalPlayer()
	{
		LocalPlayerSubsystem.Get().RegisterLocalPlayer(this);
	}

	private void UpdateSpriteVelocityAndFacing(Vector2 Input, double Delta, Vector3 MouseVector) 
	{
		Sprite.SetVelocity(Input.Length() / ((float) Delta) * WalkingSpeed);
		Sprite.ForceFlip = false;
	}

	private Vector3 GetMovedPosition(Vector3 StartPosition, Vector2 Input)
	{
		var DeltaPosition = new Vector3(0,0,0);
		DeltaPosition.X = Input.X * WalkingSpeed;
		DeltaPosition.Z = Input.Y * WalkingSpeed;

		var MovedPosition = StartPosition + DeltaPosition;
		
		return MovedPosition;
	}


	public override void _Process(double delta)
	{
		if(IsLocalPlayer)
		{
			var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown").Normalized();
			Mover.AddMovementInput(InputVector, delta);
			TickMouseInput(delta);
		}

		TickCharging(delta);
		Mover.TickUpdates(delta);
		TickBlocking(delta);

		if(HoldingBallMesh.Visible != HoldingBall)
		{
			HoldingBallMesh.Visible = HoldingBall;
		}
	}

	void TickMouseInput(double delta) 
	{
		var MousePosition = GetViewport().GetMousePosition();
		var ViewportSize = GetViewport().GetVisibleRect().Size;
		var SpaceState = GetWorld3D().DirectSpaceState;
		var ViewCamera = PlayerCamera.Get().Camera;
		var From = ViewCamera.ProjectRayOrigin(MousePosition);
		var To = From + ViewCamera.ProjectRayNormal(MousePosition) * 2000;

		var Parameters = PhysicsRayQueryParameters3D.Create(From, To);
		var results = SpaceState.IntersectRay(Parameters);

		if(results.ContainsKey("position"))
		{
			MouseWorldPosition = results["position"].As<Vector3>();
			MouseVector = MouseWorldPosition - Position;
			MouseVector.Y = 0;
			MouseVector = MouseVector.Normalized();
			DebugDraw3D.DrawSphere(results["position"].As<Vector3>(), 0.5f, Colors.Green, 0.00f);
			MouseInWindow = true;
		}
		else 
		{
			MouseInWindow = false;
		}
	}

	void TickCharging(double delta)
	{
		if(IsLocalPlayer)
		{
			if(FireButtonDown)
			{
				Mover.MovementSpeed = CachedBaseMoveSpeed * ChargingSlowFactor;
				Mover.MinSpeed = CachedMinMoveSpeed * ChargingSlowFactor;
				ChargeTime += delta;
				ChargeTime = Math.Clamp(ChargeTime, 0.0f, ChargeTimeToThrow);
			}
			else 
			{
				Mover.MinSpeed = CachedMinMoveSpeed;
				Mover.MovementSpeed = CachedBaseMoveSpeed;
				ChargeTime = 0;
			}
		}

		if(ChargeTime > 0)
		{
			if(ChargeTime > (ChargeTimeToThrow - 0.05))
			{
				DebugDraw3D.DrawSphere(Indicator.Position + Position, 0.3f, 
					Colors.Red.Lerp(Colors.Teal, (float) (ChargeTime / ChargeTimeToThrow)),
					0.1f
				);
			}
			else
				DebugDraw3D.DrawSphere(Indicator.Position + Position, 0.2f, 
					Colors.Red.Lerp(Colors.Green, (float) (ChargeTime / ChargeTimeToThrow)),
					0.1f
				);
		}
	}


	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && MouseInWindow)
		{
			if(mouseEvent.Pressed)
			{
				switch (mouseEvent.ButtonIndex)
				{
					case MouseButton.Left:
						OnFireButtonDown();
						break;
					case MouseButton.Right:
						OnCatchButtonDown();
						break;
				}
			}
			else 
			{
				switch (mouseEvent.ButtonIndex)
				{
					case MouseButton.Left:
						OnFireButtonUp();
						break;
					case MouseButton.Right:
						OnCatchButtonUp();
						break;
				}
			}
		}
	}

	void OnCatchButtonDown()
	{
		if(!IsLocalPlayer)
			return;
		// Start the catching process.
		// do a sphere trace infront of us, check to see if we hit a ball.
		
		NU.Ok("OnCatchButtonDown");

		if(!HoldingBall)
		{
			if(ScanForBall())
			{
				HoldingBallMesh.Visible = true;
			}
			else 
			{
				//StartCatching();
			}
		}
		else 
		{
			StartBlocking();
		}
	}

	void OnCatchButtonUp()
	{
	}
	
	void OnFireButtonDown()
	{
		if(HoldingBall)
		{
			FireButtonDown = true;
		}
	}

	void OnFireButtonUp()
	{
		if(!IsLocalPlayer)
		{
			return;
		}

		FireButtonDown = false;

		if(ChargeTime > ChargeTimeToThrow - 0.05)
		{
			Projectiles.FireProjectile(
				Position,
				MouseVector,
				GameNetEngine.Get().NewPredictionKey()
			);

			RpcId(1, nameof(ServerBallThrown), new Variant[]{});

			HoldingBall = false;
			HoldingBallMesh.Visible = false;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ServerBallThrown()
	{
		HoldingBall = false;
		Rpc(nameof(ServerBallThrownMulticast));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ServerBallThrownMulticast()
	{
		HoldingBall = false;
	}


	public override void _ExitTree() 
	{
		ShutDownNet();
	}

	public void SetupNetTickables()
	{
		if(OwnerId == GameSession.Get().PeerId)
		{
			GameNetEngine.Get().OnNetTick += OnNetTick;
		}

		if(GameSession.Get().IsServer())
		{
			GameNetEngine.Get().OnSyncFrame += OnSyncFrame;
		}
	}

	public void ShutDownNet() 
	{
		Mover.ShutdownNetTickables();
		Projectiles.ShutdownNetTickables();

		if(GameSession.Get().IsServer())
		{
			GameNetEngine.Get().OnSyncFrame -= OnSyncFrame;
		}
		if(OwnerId == GameSession.Get().PeerId || IsLocalPlayer)
		{
			GameNetEngine.Get().OnNetTick -= OnNetTick;
		}

	}

	public void OnNetTick(long CommandFrame, double Delta)
	{
		RpcId(1, nameof(SubmitState), new Variant[] {
			ChargeTime,
			CurrentBlockingDuration,
			BlockingDirection
		});
	}

	public void OnSyncFrame(long CommandFrame, double Delta)
	{
		// RPC to clients here
		Rpc( nameof(RecieveGameStateClient), new Variant[] {
			ChargeTime,
			CurrentBlockingDuration,
			BlockingDirection
		});
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SubmitState(
		float NewChargeTime, double NewBlockingDuration, Vector3 NewBlockingDirection
	)
	{
		var Sender = Multiplayer.GetRemoteSenderId();
		if(Sender != OwnerId)
		{
			NU.Error("Client" 
				+ Sender.ToString()
				+ " is trying to send state to me but i'm owned by " + OwnerId.ToString() );
			return;
		}

		if(NewBlockingDuration > 0 && CurrentBlockingDuration <= 0 && CurrentBlockingCooldown < 0.5)
		{
			CurrentBlockingDuration = NewBlockingDuration;
			BlockingDirection = NewBlockingDirection;
			CurrentBlockingCooldown = BlockingCooldown;
		}

		ChargeTime = NewChargeTime;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(
		float NewChargeTime, double NewBlockingDuration, Vector3 NewBlockingDirection)
	{
		if(OwnerId != GameSession.Get().PeerId)
		{
			ChargeTime = NewChargeTime;
			BlockingDirection = NewBlockingDirection;
			CurrentBlockingDuration = NewBlockingDuration;
		}
	}


	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void ClientRecieveAddImpulse(
			Vector2 Impulse)
	{
		if(IsLocalPlayer)
		{
			Mover.Velocity += Impulse;
		}
	}

	public void ServerAddImpulse(Vector2 Impulse)
	{
		Rpc(nameof(ClientRecieveAddImpulse), new Variant[]{
			Impulse,
		});
	}

	public Array<Dictionary> DoShapeTrace(Rid ShapeRid, Vector3 Position)
	{
		var SpaceState = GetWorld3D().DirectSpaceState;
		var Parameters = new PhysicsShapeQueryParameters3D();

		Parameters.ShapeRid = ShapeRid;
		var StartTransform = new Transform3D(new Basis(1,0,0,0,1,0,0,0,1), Position);

		DebugDraw3D.DrawSphere(Position, PickupRadius, Colors.Magenta, 5.0f);

		Parameters.Transform = StartTransform;
		Parameters.Exclude.Add(GetRid());

		return SpaceState.IntersectShape(Parameters);
	}

	// ============== Dodging ===============

	public bool CheckDodging()
	{
		return false;
	}

	// ============== Catching ===============
	public bool CheckCatching(Vector3 BallPosition)
	{
        if(!HoldingBall)
        {
            if(CurrentBlockingDuration > 0)
            {
                var Dir = (BallPosition - Position).Normalized();
                if(Dir.Dot(BlockingDirection) > 0.5)
                {
                    return true;
                }
            }
        }
		return false;
	}

	// ============== Blocking ===============
	[Export] double BlockingCooldown = 10.0;
	double CurrentBlockingCooldown = 0.0;
	[Export] double BlockingDuration = 5.0;
	[Export] double CatchingDuration = 5.0;
	double CurrentBlockingDUration = 0.0;
	Vector3 BlockingDirection;

	public bool CheckBlocking(Vector3 BallPosition)
	{
        if(HoldingBall)
        {
            if(CurrentBlockingDuration > 0)
            {
                var Dir = (BallPosition - Position).Normalized();
                if(Dir.Dot(BlockingDirection) > 0.5)
                {
                    return true;
                }
            }
        }
		return false;
	}

	void TickBlocking(double delta)
	{
		if(CurrentBlockingDuration > 0)
		{
			CurrentBlockingDuration -= delta;
			DebugDraw3D.DrawSphere(Position + BlockingDirection * 0.5f, 0.5f, Colors.Blue, 0.00f);
		}
		else if(CurrentBlockingCooldown > 0)
		{
			DebugDraw3D.DrawSphere(HoldingBallMesh.GlobalPosition, 0.3f, Colors.Red.Lerp(Colors.Green, ((float)CurrentBlockingCooldown) / ((float)BlockingCooldown)), 0.00f);
			CurrentBlockingCooldown -= delta;
		}
	}

    void StartCatching()
    {
		if(CurrentBlockingCooldown <= 0)
		{
			CurrentBlockingDuration = CatchingDuration;
			CurrentBlockingCooldown = BlockingCooldown;
			BlockingDirection = MouseVector;
		}
    }

	void StartBlocking() 
	{
		if(CurrentBlockingCooldown <= 0)
		{
			CurrentBlockingDuration = BlockingDuration;
			CurrentBlockingCooldown = BlockingCooldown;
			BlockingDirection = MouseVector;
		}
	}

	// =======
	public void PlayerKnockedOut()
	{
		// Gets added the server's GameLevelState of players who have been knocked the fuck out.
		// Player is invisble, can still move around but is functionally a ghost
	}

	public void PlayerRespawn(Vector3 RespawnPosition)
	{
		// Called by server's GameState, respawns the player on one side of the court.
	}

	public bool ScanForBall() 
	{
		var Result = DoShapeTrace(PickupShapeRid, Position + MouseVector * 0.5f);
		foreach(var R in Result)
		{
			var colliderAsWorldBall = R["collider"].Obj as WorldBall;
			if(colliderAsWorldBall != null)
			{
				colliderAsWorldBall.GetPickedUp(this);
				PickedUpBall = colliderAsWorldBall;
				NU.Ok("Picked up a ball");
				HoldingBall = true;
				return true;
			}
		}

		return false;
	}
}
