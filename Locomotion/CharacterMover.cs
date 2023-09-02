using Godot;
using System;

public partial class CharacterMover: Node
{
    public long OwnerId = 0;
    [Export] public float MovementSpeed = 2.5f;

    [Export] public float ServerMovementLeniency = 0.3f;
    [Export] public float MaxPredictionDesyncLength = 0.3f;
    [Export] public float PredictionDesyncLerpScale = 0.3f;
    
    [Export] public Node3D Self;

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

    public void SetOwner(long NewOwnerId) 
    {
        OwnerId = NewOwnerId;
        if(OwnerId == GameSession.Get().PeerId)
        {
            LocallyControlled = true;
        }
        else 
        {
            LocallyControlled = false;
        }
    }

    // Have this get called from the owner's _Process function, at the end.
    // do it after all AddMovementInput()s are called
    public void TickUpdate(double Delta)
    {
        if(NetTickTime < 0)
        {
            NetTickTime = GameNetEngine.Get().TickDelta;
            // OnNetTick(0, GameNetEngine.Get().TickDelta);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void SubmitMove(long CommandFrame, Vector2 NewMove, Vector3 ProposedPosition)
    {
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

    // Core movement logic
    private Vector3 SimulateMovedPosition(
            Vector3 StartPosition,
            Vector2 MovementInput,
            float MovementSpeed)
    {
        var DeltaPosition = new Vector3(0,0,0);
		DeltaPosition.X = MovementInput.X * MovementSpeed;
		DeltaPosition.Z = MovementInput.Y * MovementSpeed;

		var MovedPosition = StartPosition + DeltaPosition;
        // TODO: do collision resolution here.
        return MovedPosition;
    }

    public void ShutdownNet() 
    {
    }
}
