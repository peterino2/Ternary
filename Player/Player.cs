using Godot;
using System;

public partial class Player : CharacterBody3D
{
	AnimatedGameSprite Sprite;

	[Export] CharacterMover Mover;

	[Export] public float WalkingSpeed = 2.5f;
	[Export] public long OwnerId = 0;
	[Export] public Node3D ArrowBase;
	[Export] public MultiplayerSynchronizer Sync;

	// State variables
	[Export] public Vector3 PositionSync = new Vector3(0,0,0);
	[Export] public Vector2 NetMoveSync = new Vector2(0,0);

	Vector2 AccumulatedMovement = new Vector2(0,0);
	Vector2 SyncAccumulatedMovement = new Vector2(0,0);
	Vector2 LocalInput = new Vector2(0,0);
	Vector2 MouseVector = new Vector2(0,0);
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

	private void UpdateSpriteVelocityAndFacing(Vector2 Input, double Delta, Vector2 MouseVector) 
	{
		Sprite.SetVelocity(Input.Length() / ((float) Delta) * WalkingSpeed);

		Sprite.ForceFlip = false;
		float deadzone = 0.2f;

		if(MouseVector.X > deadzone && MouseVector.Y > deadzone)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Down);
		}
		if(MouseVector.X > -deadzone && MouseVector.Y < -deadzone)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Up);
		}
		if(MouseVector.X < -deadzone && MouseVector.Y < -deadzone)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Up);
			Sprite.ForceFlip = true;
		}
		if(MouseVector.X < -deadzone && MouseVector.Y > deadzone)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Left);
		}

		if(IsLocalPlayer)
			ArrowBase.Rotation = new Vector3(0, (float)Math.Atan2(-MouseVector.Y, MouseVector.X) + 0.5f * (float) Math.PI, 0);

		// -- +- 
		// -+ ++

		/*
		if(MouseVector.X > 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Right);
		}
		if(MouseVector.X < 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Left);
		}
		if(MouseVector.Y > 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Down);
		}
		if(MouseVector.Y < 0.0f)
		{
			Sprite.SetFacingDir(AnimatedGameSprite.FacingDirection.Up);
		}
		*/
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
		if(IsLocalPlayer)
		{
			var InputVector = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown").Normalized();
			Mover.AddMovementInput(InputVector, delta);
		}

		Mover.TickUpdates(delta);

		if(!GameSession.Get().IsServer())
		{
			var MousePosition = GetViewport().GetMousePosition();
			var ViewportSize = GetViewport().GetVisibleRect().Size;
			MouseVector = MousePosition - (ViewportSize / 2.0f);
			MouseVector = MouseVector.Normalized();

			// UpdateSpriteVelocityAndFacing(Mover.GetLastMove(), Mover.LastDelta, MouseVector);
		}
	}

	public override void _ExitTree() 
	{
		ShutDownNet();
	}

	public void ShutDownNet() 
	{
		NU.Warning("shutting down net tickables.");
		Mover.ShutdownNetTickables();
	}
}
