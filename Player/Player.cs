using Godot;
using System;

public partial class Player : Node3D
{
	AnimatedGameSprite Sprite;

	[Export] CharacterMover Mover;

	[Export] public float WalkingSpeed = 2.5f;
	[Export] public long OwnerId = 0;
	[Export] public MultiplayerSynchronizer Sync;

	// State variables
	[Export] public Vector3 PositionSync = new Vector3(0,0,0);
	[Export] public Vector2 NetMoveSync = new Vector2(0,0);

	Vector2 AccumulatedMovement = new Vector2(0,0);
	Vector2 SyncAccumulatedMovement = new Vector2(0,0);
	Vector2 LocalInput = new Vector2(0,0);
	Vector3 LastDeltaPosition = new Vector3(0,0,0);

	[Export] bool UsePrediction = true;

	public bool IsLocalPlayer = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Sprite = GetNode<AnimatedGameSprite>("Node3D/Sprite");
		Sprite.Play("IdleDown");

		if(OwnerId == GameSession.Get().PeerId)
		{
			RegisterAsLocalPlayer();
			IsLocalPlayer = true;
		}

		Mover.SetOwner(OwnerId);
        Mover.SetBase(this);
        Mover.SetupNetTickables();
	}

	// This function is only called on the server side,
	// It is called when the server is ready to broadcast state for a given command frame.
	public void OnSyncFrame(long CommandFrame, double Delta)
	{
		Rpc(nameof(RecieveGameStateClient), new Variant[]{CommandFrame, PositionSync, NetMoveSync});
	}

	// This function is callled when the local client is ready to submit a list of accumulated movement for a given command frame.
	public void OnNetTick(long CommandFrame, double NetTickDelta)
	{
		if(IsLocalPlayer)
		{
			RpcId(1, nameof(SubmitMove), new Variant[] {
                CommandFrame,
                AccumulatedMovement,
                Position
            });
			AccumulatedMovement = new Vector2(0,0);
		}
	}

	public void OnSynchronized()
	{
		if(!IsLocalPlayer)
		{
			UpdateSpriteVelocityAndFacing(NetMoveSync, GameNetEngine.Get().TickDelta);
			return;
		}

		SyncAccumulatedMovement = new Vector2(0, 0);
	}

	// This function is called on all clients when the server broadcasts the
	// state of this class for a given command Frame
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(long CommandFrame, Vector3 NewPositionSync, Vector2 NewNetMoveSync)
	{
		PositionSync = NewPositionSync;
		NetMoveSync = NewNetMoveSync;

		if(!IsLocalPlayer)
		{
			UpdateSpriteVelocityAndFacing(NetMoveSync, GameNetEngine.Get().TickDelta);
			return;
		}

		SyncAccumulatedMovement = new Vector2(0, 0);
	}

	public void SetOwnerServer(long NewOwner)
	{
		OwnerId = NewOwner;
	}

	// Called by Server to Update the Ownership of a given actor
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetOwnerOnClient(long NewOwner)
	{
		OwnerId = NewOwner;

		if(NewOwner == GameSession.Get().PeerId)
		{
			RegisterAsLocalPlayer();
		}
	}

	// Sent from client to server, via RpcId(1...
	// This sends forward our accumulated movement vector along with a proosed position, that 
	// the client believes we've moved to
	//
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void SubmitMove(long CommandFrame, Vector2 NewMove, Vector3 ProposedPosition)
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
		Position = GetMovedPosition(Position, AccumulatedMovement);

		// If the client's proposed position is reachable and within an error tolerance.
		// we can take it at it's word and move there.
		if((ProposedPosition - Position).Length() < 0.3f * (float)GameNetEngine.Get().TickDelta)
		{
			Position = ProposedPosition;
		}
		UpdateSpriteVelocityAndFacing(AccumulatedMovement, GameNetEngine.Get().TickDelta);
		NetMoveSync = AccumulatedMovement;
		PositionSync = Position;
	}

	public void RegisterAsLocalPlayer()
	{
		LocalPlayerSubsystem.Get().RegisterLocalPlayer(this);
	}

	private void UpdateSpriteVelocityAndFacing (Vector2 Input, double Delta) 
	{
		Sprite.SetVelocity(Input.Length() / ((float) Delta) * WalkingSpeed);

		if(Input.Y > 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Down);
		}
		if(Input.Y < 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Up);
		}
		if(Input.X > 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Right);
		}
		if(Input.X < 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Left);
		}
	}

	private Vector3 GetMovedPosition(Vector3 StartPosition, Vector2 Input)
	{
		var DeltaPosition = new Vector3(0,0,0);
		DeltaPosition.X = Input.X * WalkingSpeed;
		DeltaPosition.Z = Input.Y * WalkingSpeed;

		var MovedPosition = StartPosition + DeltaPosition;
		
		return MovedPosition;
	}
	double NetTickTime = 0;

	public override void _Process(double delta)
	{
		var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown").Normalized();
        Mover.AddMovementInput(InputVector, delta);
        Mover.TickUpdates(delta);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
    /*
	public override void _Process(double delta)
	{
		if(!IsLocalPlayer)
		{
			TickInterpolation(delta);
			return;
		}

		ProcessInputs(delta);
		TickPrediction(delta);

		NetTickTime -= delta;
		if(NetTickTime < 0)
		{
			NetTickTime = GameNetEngine.Get().TickDelta;
			OnNetTick(0, GameNetEngine.Get().TickDelta);
		}
	}
    */

	private void TickInterpolation(double delta)
	{
		if((PositionSync - Position).Length() < (float) delta * WalkingSpeed)
		{
			Position = PositionSync;
		}
		else {
			Position = Position + (PositionSync - Position).Normalized() * (float) delta * WalkingSpeed;
		}
	}

	private void TickPrediction(double delta)
	{
		var ServerPredictedPosition = GetMovedPosition(PositionSync, SyncAccumulatedMovement);
		UpdateSpriteVelocityAndFacing(LocalInput, delta);
		if(UsePrediction)
		{
			Position = GetMovedPosition(Position, LocalInput);
			Vector3 NetError = Position - (ServerPredictedPosition);
			if(NetError.Length() > 0.3f)
			{
				Position = Position.Lerp(ServerPredictedPosition, 0.4f);
			}
		}
		else 
		{
			Position = ServerPredictedPosition;
		}
	}

    /*
	private void ProcessInputs(double delta)
	{
		// Accumulate Inputs
		var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");
		InputVector = InputVector.Normalized();
		LocalInput = InputVector * (float)delta;
		AccumulatedMovement += LocalInput;
		SyncAccumulatedMovement += LocalInput;
	}
    */

	public void ShutDownNet() 
	{
        Mover.ShutDownNetTickables();
	}
}
