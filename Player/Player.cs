using Godot;
using Godot.Collections;
using System;

public partial class Player : CharacterBody3D
{
	AnimatedGameSprite Sprite;

	[Export] public CharacterMover Mover;
	[Export] ProjectileSpawner Projectiles;

	[Export] public float WalkingSpeed = 2.5f;
	[Export] public long OwnerId = 0;
	[Export] public Node3D ArrowBase;
	[Export] public MultiplayerSynchronizer Sync;
	[Export] public PackedScene BallScene;

	[Export] public Node3D Indicator;
	[Export] public MeshInstance3D HoldingBallMesh;
	[Export] public Node3D DogeMeshBase;
	[Export] public PackedScene DeathVFX;
	[Export] public GpuParticles3D HyperEyes;

	public bool Emoting = false;

	[Export] public Node3D Team1Hat;
	[Export] public Node3D Team2Hat;

	[Export] public double BlockingCooldown = 10.0;
	public double CurrentBlockingCooldown = 0.0;
	[Export] public double BlockingDuration = 5.0;
	[Export] public double CatchingDuration = 5.0;
	public double CurrentBlockingDuration = 0.0;
	Vector3 BlockingDirection;
	[Export] AnimationPlayer DogeMeshAnimplayer;

	public WorldBall PickedUpBall;

	Vector2 AccumulatedMovement = new Vector2(0,0);
	Vector2 SyncAccumulatedMovement = new Vector2(0,0);
	Vector2 LocalInput = new Vector2(0,0);
	Vector3 MouseVector = new Vector3(0,0,0);
	Vector3 MouseWorldPosition;
	Vector3 LastDeltaPosition = new Vector3(0,0,0);
	Vector3 ThrowQueuedVector;

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

	[Export] public int TeamId = 1;
	int OldTeamId = 1;
	public bool IsDead = false;

	[Export] public double ChargeTimeToThrow = 1.0;
	public double ChargeTime = 0.0;

	[Export] bool UsePrediction = true;

	public bool IsLocalPlayer = false;
	public double ThrowTime = 0.0;
	public bool ThrowQueued = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Sprite = GetNode<AnimatedGameSprite>("Node3D/Sprite");
		Sprite.Play("IdleDown");
		DogeMeshAnimplayer.Play("Armature|Idle");

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

	private void UpdateVelocityAndFacing(double delta) 
	{
		var velocity = Mover.Velocity;
		var Anim = "Armature|Idle";

		if(Emoting)
		{
			Anim = "Armature|Emote";
		}

		if(velocity.Length() > 0.4)
		{
			Anim = "Armature|Run";
			Emoting = false;
			DogeMeshAnimplayer.SpeedScale = 1.0f;
			Transform3D transform = DogeMeshBase.Transform;
			transform.Basis = Basis.Identity;
			transform = transform.Scaled(new Vector3(0.5f, 0.5f, 0.5f));
			transform = transform.Rotated(Vector3.Up, (float) Math.Atan2( velocity.X, velocity.Y )); // first rotate about Y
			DogeMeshBase.Transform = transform;
		}
		if(ThrowTime > 0)
		{
			Anim = "Armature|Throw";
			Emoting = false;
			ThrowTime -= delta;
			DogeMeshAnimplayer.SpeedScale = 1.0f;
		}
		if(CurrentDodgingDuration > 0)
		{
			Anim = "Armature|Dodge";
			Emoting = false;
			DogeMeshAnimplayer.SpeedScale = 1.5f;
		}
		if(CurrentBlockingDuration > 0)
		{
			Anim = "Armature|Throw";
			Emoting = false;
			DogeMeshAnimplayer.SpeedScale = 1.0f;
			ArrowBase.Visible = true;

			Transform3D transform = ArrowBase.Transform;
			transform.Basis = Basis.Identity;
			transform = transform.Rotated(Vector3.Up, (float) Math.Atan2( MouseVector.X, MouseVector.Z )); // first rotate about Y
			ArrowBase.Transform = transform;
		}
		else 
		{
			ArrowBase.Visible = false;
		}
		DogeMeshAnimplayer.Play(Anim);
	}

	private Vector3 GetMovedPosition(Vector3 StartPosition, Vector2 Input)
	{
		var DeltaPosition = new Vector3(0,0,0);
		DeltaPosition.X = Input.X * WalkingSpeed;
		DeltaPosition.Z = Input.Y * WalkingSpeed;

		var MovedPosition = StartPosition + DeltaPosition;
		
		return MovedPosition;
	}


	Vector3 MovementVector;

	public override void _Process(double delta)
	{
		if(IsLocalPlayer)
		{
			var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown").Normalized();
			MovementVector = new Vector3(InputVector.X, 0, InputVector.Y);
			Mover.AddMovementInput(InputVector, delta);
			TickMouseInput(delta);

			var DodgeAction = Input.GetActionStrength("Dodge");
			if(DodgeAction > 0.5 && CurrentDodgingCooldown <= 0 && !IsDead)
			{
				NU.Ok("Dodging started");
				if(MovementVector.Length() > 0.1)
				{
					StartDodging(MovementVector);
				}
				else
				{
					StartDodging(MouseVector);
				}
			}

			var EmoteAction = Input.GetActionStrength("Emote");
			if(EmoteAction > 0.2 && !IsDead)
			{
				StartEmote();
			}

			var OpenScore = Input.GetActionStrength("OpenScore");
			if(OpenScore > 0.2 && !ScoreOpen)
			{
				OnOpenScore();
			}
			else if(ScoreOpen &&  OpenScore < 0.1)
			{
				OnCloseScore();
			}
		}

		if(OldTeamId != TeamId)
		{
			UpdateTeamVisuals();
			OldTeamId = TeamId;
		}

		TickCharging(delta);
		Mover.TickUpdates(delta);
		UpdateVelocityAndFacing(delta);

		if(IsDead)
			return;
		TickBlocking(delta);
		TickDodging(delta);

		if(TeamId == 1)
		{
			DebugDraw3D.DrawSphere(GlobalPosition, 0.5f, Colors.Blue, 0.00f);
		}

		if(TeamId == 2)
		{
			DebugDraw3D.DrawSphere(GlobalPosition, 0.5f, Colors.Red, 0.00f);
		}

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


	bool ScoreOpen = false;
	void OnOpenScore()
	{
		ScoreOpen = true;
	}

	void OnCloseScore()
	{
		ScoreOpen = false;
	}

	
	void TickCharging(double delta)
	{

		if(GameState.Get().GameTime < 30)
		{
			HyperEyes.Emitting = true;
		}
		else 
		{
			HyperEyes.Emitting = false;
		}
		if(IsLocalPlayer)
		{
			if(FireButtonDown)
			{
				Mover.MovementSpeed = CachedBaseMoveSpeed * ChargingSlowFactor;
				Mover.MinSpeed = CachedMinMoveSpeed * ChargingSlowFactor;
				ChargeTime += delta;
				if(GameState.Get().GameTime < 30)
				{
					ChargeTime += delta * 2;
				}
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

		if(IsLocalPlayer && ThrowQueued)
		{
			if(ThrowTime < 0.8)
			{
				ThrowQueued = false;
				Projectiles.FireProjectile(
					Position,
					ThrowQueuedVector,
					GameNetEngine.Get().NewPredictionKey()
				);
			}
		}
	}


	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && MouseInWindow)
		{
			if(mouseEvent.Pressed)
			{
				if(!IsDead)
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
			}
			else 
			{
				if(!IsDead)
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
			if(!(CurrentDodgingDuration > 0))
			{
				StartCatching();
			}
		}
		else if(!(CurrentDodgingDuration > 0))
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
		else 
		{
			ScanForBall();
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

			ThrowQueuedVector = MouseVector;
			ThrowQueued = true;
			ThrowTime = 1.20;

			RpcId(1, nameof(ServerBallThrown), new Variant[]{});

			HoldingBall = false;
			HoldingBallMesh.Visible = false;
			VelocityOverride(MouseVector, 5.0f, 0.2);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ServerBallThrown()
	{
		HoldingBall = false;
		ThrowTime = 1.20;
		Rpc(nameof(ServerBallThrownMulticast));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ServerBallThrownMulticast()
	{
		ThrowTime = 1.20;
		HoldingBall = false;
	}


	public override void _ExitTree() 
	{
		ShutDownNet();
	}

	public void UpdateTeamVisuals()
	{
		if(TeamId == 0)
		{
			Team1Hat.Visible = false;
			Team2Hat.Visible = false;
		}
		if(TeamId == 1)
		{
			Team1Hat.Visible = true;
			Team2Hat.Visible = false;
		}
		else if(TeamId == 2)
		{
			Team1Hat.Visible = false;
			Team2Hat.Visible = true;
		}
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

		if(IsLocalPlayer)
		{
			IngameUI.Get().SetupPlayer(this);
		}

		GameState.Get().AvatarSpawnedLocal[OwnerId] = this;

		SetTeam(TeamId);
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
			BlockingDirection,
			CurrentDodgingDuration
		});
	}

	public void OnSyncFrame(long CommandFrame, double Delta)
	{
		// RPC to clients here
		Rpc( nameof(RecieveGameStateClient), new Variant[] {
			ChargeTime,
			CurrentBlockingDuration,
			BlockingDirection,
			HoldingBall,
			CurrentDodgingDuration,
		});
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SubmitState(
		float NewChargeTime, double NewBlockingDuration, Vector3 NewBlockingDirection, double NewDodgeDuration
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

		CurrentDodgingDuration = NewDodgeDuration;
		ChargeTime = NewChargeTime;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(
		float NewChargeTime, double NewBlockingDuration, Vector3 NewBlockingDirection, 
		bool NewHoldingBall, double NewDodgingDuration)
	{
		if(OwnerId != GameSession.Get().PeerId)
		{
			ChargeTime = NewChargeTime;
			BlockingDirection = NewBlockingDirection;
			CurrentDodgingDuration = NewDodgingDuration;
			CurrentBlockingDuration = NewBlockingDuration;
			HoldingBall = NewHoldingBall;
			if(HoldingBall)
			{
				HoldingBallMesh.Visible = true;
			}
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

	[Export] public double DodgeDuration = 0.2;
	public double CurrentDodgingDuration = 0.0;
	[Export] public double DodgingCooldown = 3.0;
	public double CurrentDodgingCooldown = 0.0;

	[Export] float DodgeImpulse = 15.0f;

	public bool CheckDodging()
	{
		if(CurrentDodgingDuration < 0)
		{
			return true;
		}
		return false;
	}

	public void VelocityOverride(Vector3 Direction, float strength, double Duration)
	{
		Mover.DodgeVelocityOverride = new Vector2(Direction.X, Direction.Z) * strength;
		Mover.DodgeVelocityOverrideDuration = Duration;
	}

	public void StartDodging(Vector3 DodgeDirection)
	{
		if(CurrentDodgingCooldown <= 0.0)
		{
			CurrentDodgingCooldown = DodgingCooldown;
			CurrentDodgingDuration = DodgeDuration;
			Mover.DodgeVelocityOverride = new Vector2(DodgeDirection.X, DodgeDirection.Z) * DodgeImpulse;
			Mover.DodgeVelocityOverrideDuration = DodgeDuration;
		}
	}

	void TickDodging(double delta)
	{
		if(CurrentDodgingDuration > 0 )
		{
			CurrentDodgingDuration -= delta;
			if(IsLocalPlayer)
			{
				ChargeTime = 0;
			}
		}
		if(CurrentDodgingCooldown > 0)
		{
			CurrentDodgingCooldown -= delta;
		}
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
				HoldingBallMesh.Visible = true;
				return true;
			}
		}

		return false;
	}

	// catching functions
	
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void RecievedBallCaughtEvent(String BallPath)
	{
		var BallRef = GetNode<WorldBall>(BallPath);
		HoldingBall = true;
		PickedUpBall = BallRef;
		HoldingBallMesh.Visible = true;
	}
	
	// called on the server when the ball is caught by this player
	public void PlayerCaughtBallOnServer(WorldBall BallRef)
	{
		RpcId(OwnerId, nameof(RecievedBallCaughtEvent), new Variant[] {
			BallRef.GetPath()
		});

		HoldingBall = true;
		PickedUpBall = BallRef;
		HoldingBallMesh.Visible = true;
	}

	public void ReviveMeLocal()
	{
		IsDead = false;
		Visible = true;
		CollisionLayer = 0x1 | (0x1 << 4) | (1 << 3);
		CollisionMask = 0x1 | (0x1 << 4) | (1 << 3);

		Mover.Ghosting = false;
		HoldingBall = false;
		HoldingBallMesh.Visible = false;
		PickedUpBall = null;
		ChargeTime = 0;
		CurrentDodgingDuration = 0;
		CurrentBlockingDuration = 0;
		ThrowQueued = false;
		ThrowTime = 0;
	}

	public void KillMeLocal()
	{
		IsDead = true;
		Visible = false;
		CollisionLayer = 0x0;
		CollisionMask = 0x0;
		Mover.Ghosting = true;

		ChargeTime = 0;
		CurrentDodgingDuration = 0;
		CurrentBlockingDuration = 0;
		HoldingBall = false;
		PickedUpBall = null;
		ThrowQueued = false;
		ThrowTime = 0;

		var vfx = DeathVFX.Instantiate() as Node3D;
		vfx.Position = GlobalPosition;

		GameState.Get().LevelNode.AddChild(vfx);
	}

	public void KillMeServer()
	{
		KillMeLocal();
		Rpc(nameof(KillMeMulticast), new Variant[]{});
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void KillMeMulticast()
	{
		KillMeLocal();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ReviveMeMulticast()
	{
		ReviveMeLocal();
	}

	public void ReviveMeServer(Vector3 RevivePosition)
	{
		ReviveMeLocal();
		Mover.OverridePosition(RevivePosition);
		Rpc(nameof(ReviveMeMulticast), new Variant[]{});
	}

	public void SetTeam(int NewTeamId)
	{
		TeamId = NewTeamId;
		UpdateTeamVisuals();

		if(TeamId == 0)
		{
			Mover.SetClientAuthorityLocal(true);
		}
		else 
		{
			Mover.SetClientAuthorityLocal(false);
		}
	}

	public bool IsDodging()
	{
		return CurrentDodgingDuration > 0;
	}

	void StartEmote()
	{
		NU.Ok("Emoting!!!");
		if(IsLocalPlayer)
		{
			Emoting = true;
			RpcId(1, nameof(StartEmoteServer), new Variant[]{});
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartEmoteServer()
	{
		Emoting = true;
		Rpc(nameof(StartEmoteMulticast), new Variant[]{});
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void StartEmoteMulticast()
	{
		if(!IsLocalPlayer)
		{
			Emoting = true;
		}
	}
}


