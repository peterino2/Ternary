using Godot;
using System;

public partial class Player : Node3D
{
	AnimatedGameSprite Sprite;
	[Export] public float WalkingSpeed = 2.5f;
	[Export] public long OwnerId = 0;
    [Export] public MultiplayerSynchronizer Sync;
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

    public void OnSynchronized()
    {
        NU.Ok("Synchronized with info from server? " + OwnerId);
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

	public void RegisterAsLocalPlayer()
	{
		LocalPlayerSubsystem.Get().RegisterLocalPlayer(this);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if(!IsLocalPlayer)
        {
            return;
        }
		var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");
		Vector3 Velocity = new Vector3(0,0,0);
		Velocity.X = InputVector.X * (float)delta * WalkingSpeed;
		Velocity.Z = InputVector.Y * (float)delta * WalkingSpeed;
		Position = Position + Velocity;
		
		Sprite.SetVelocity(InputVector.Length() * WalkingSpeed);

		if(InputVector.Y > 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Down);
		}
		if(InputVector.Y < 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Up);
		}
		if(InputVector.X > 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Right);
		}
		if(InputVector.X < 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Left);
		}
	}
}
