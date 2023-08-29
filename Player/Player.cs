using Godot;
using System;

public partial class Player : Node3D
{
	AnimatedGameSprite Sprite;
	[Export] public float WalkingSpeed = 2.5f;
	[Export] public long OwnerId = 0;
    [Export] public MultiplayerSynchronizer Sync;
    [Export] public Vector3 PositionSync = new Vector3(0,0,0);
    [Export] public Vector2 NetMoveSync = new Vector2(0,0);

    Vector2 AccumulatedMovement = new Vector2(0,0);
    Vector2 LocalInput = new Vector2(0,0);
    Vector3 LastDeltaPosition = new Vector3(0,0,0);

    [Export] bool UsePrediction = true;

	public bool IsLocalPlayer = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Sprite = GetNode<AnimatedGameSprite>("Node3D/Sprite");
		Sprite.Play("IdleDown");
        Sync.Synchronized += OnSynchronized;

        if(OwnerId == GameSession.Get().PeerId)
        {
            RegisterAsLocalPlayer();
            IsLocalPlayer = true;
        }
	}

    public void OnNetTick(long CommandFrame, double NetTickDelta)
    {
        if(IsLocalPlayer)
        {
            RpcId(1, nameof(SubmitMove), new Variant[] {CommandFrame, AccumulatedMovement, Position});
            AccumulatedMovement = new Vector2(0,0);
        }
    }

    public void OnSynchronized()
    {
        if(!IsLocalPlayer)
        {
            UpdateSpriteVelocityAndFacing(NetMoveSync, GameNetEngine.Get().TickDelta);
            Position = PositionSync;
        }
    }

	public void SetOwnerServer(long NewOwner)
	{
		OwnerId = NewOwner;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SetOwnerOnClient(long NewOwner)
	{
		OwnerId = NewOwner;

		if(NewOwner == GameSession.Get().PeerId)
		{
			RegisterAsLocalPlayer();
		}
	}

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
        if(Sender == OwnerId)
        {
            AccumulatedMovement = NewMove;
            Position = GetMovedPosition(Position, AccumulatedMovement);
            if((ProposedPosition - Position).Length() < 0.2f * (float)GameNetEngine.Get().TickDelta)
            {
                Position = ProposedPosition;
            }
            UpdateSpriteVelocityAndFacing(AccumulatedMovement, GameNetEngine.Get().TickDelta);
            NetMoveSync = AccumulatedMovement;
            PositionSync = Position;
        }
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

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if(!IsLocalPlayer)
        {
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

    private void TickPrediction(double delta)
    {
        if(UsePrediction)
        {
            Position = GetMovedPosition(Position, LocalInput);
            UpdateSpriteVelocityAndFacing(LocalInput, delta);
            var ServerPredictedPosition = GetMovedPosition(PositionSync, AccumulatedMovement);

            Vector3 NetError = Position - (ServerPredictedPosition);
            if(NetError.Length() > 0.2f)
            {
                Position = Position.Lerp(ServerPredictedPosition, 0.4f);
            }
        }
    }

    private void ProcessInputs(double delta)
    {
        // Accumulate Inputs
		var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");
        InputVector = InputVector.Normalized();
        LocalInput = InputVector * (float)delta;

        AccumulatedMovement += new Vector2(InputVector.X * (float) delta, InputVector.Y * (float) delta);
    }
}
