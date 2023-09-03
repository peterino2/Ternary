using Godot;
using System;

public partial class CharacterMover: Node
{
	public long OwnerId = 0;
	[Export] public float MovementSpeed = 2.5f;

	// === settings ===
	[Export] public float ServerMovementLeniency = 0.3f;
	[Export] public float MaxPredictionDesyncLength = 0.3f;
	[Export] public float PredictionDesyncLerpScale = 0.3f;
	
	[Export] public Node3D Base;

	[Export] public Vector3 PositionSync = new Vector3(0,0,0);
	[Export] public Vector2 NetMoveSync = new Vector2(0,0);

	// Input Vector scaled for this last frame
	Vector2 LocalInput = new Vector2(0,0);

	// Accumulated sum of inputs since the last move submission
	Vector2 AccumulatedMovement = new Vector2(0,0);

	// Accumulated sum of inputs since the last command frame state
	Vector2 AccumulatedMovementSinceSync = new Vector2(0,0);

	public bool LocallyControlled = false;
	private double NetTickTime  = 0;

	public override void _Ready()
	{
	}

    // call me from your object's ready after my setup is complete
	public void SetupNetTickables()
	{
        if(OwnerId == GameSession.Get().OwnerId)
        {
            GameNetEngine.Get().OnSyncFrame += OnSyncFrame;
        }

        if(GameSession.Get().IsServer())
        {
            GameNetEngine.Get().OnNetTick += OnNetTick;
        }
	}

    public void SetBase(Node3D NewBase)
    {
        Base = NewBase;
        PositionSync = Base.Position;
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
            Position
        });
    }

	public void SetOwner(long NewOwnerId) 
	{
		OwnerId = NewOwnerId;
		LocallyControlled = (OwnerId == GameSession.Get().PeerId);
	}

	// Have this get called from the owner's _Process function, at the end.
	// do it after all AddMovementInput()s are called
    // updates every frame
	public void TickUpdate(double Delta)
	{
	}
    
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

	// This function is called on all clients when the server broadcasts the
	// state of this class for a given command Frame
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(long CommandFrame, Vector3 NewPositionSync, Vector2 NewNetMoveSync)
	{
		PositionSync = NewPositionSync;
		NetMoveSync = NewNetMoveSync;
		SyncAccumulatedMovement = new Vector2(0, 0);
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
		Position = SimulateMovedPosition(Position, AccumulatedMovement, MovementSpeed);

        if((Proposed Position -  Position).Length() < 0.3f * (float) GameNetEngine.Get().TickDelta)
        {
            Position = ProposedPosition;
        }
        NetMoveSync = AccumulatedMovement;
        PositionSync = Position;
	}

	// Adds basic movement input, use axis values and delta time.
	// you typically want to use this one.
	public void AddMovementInput(Vector2 MovementInput, double delta)
	{
		AddMovementInputRaw(MovementInput.Normalized() * (float) delta);
	}

	// Low level adds an accumulated value to the movement input
	public void AddMovementInputRaw(Vector2 MovementInput)
	{
		LocalInput = MovementInput;
		AccumulatedMovement += MovementInput;
		AccumulatedMovementSinceSync += MovementInput;
	}

    public void TickPrediction(double delta)
    {
    }

	// Core movement logic
	private Vector3 SimulateMovedPosition(
			Vector3 StartPosition,
			Vector2 MovementInput,
			float SimulatedMovementSpeed)
	{
		var DeltaPosition = new Vector3(
            MovementInput.X * SimulatedMovementSpeed,
            MovementInput.Y * SimulatedMovementSpeed, 
            0);

		var MovedPosition = StartPosition + DeltaPosition;
		// TODO: do collision resolution here.
		return MovedPosition;
	}

	public void ShutdownNetTickables() 
	{
        if(OwnerId == GameSession.Get())
        {
            GameNetEngine.Get().OnNetTick -= OnNetTick;
        }
        if(GameSession.Get().IsServer())
        {
            GameNetEngine.Get().OnSyncFrame -= OnSyncFrame;
        }
	}
}
