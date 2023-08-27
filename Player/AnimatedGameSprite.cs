using Godot;
using System;

public partial class AnimatedGameSprite : AnimatedSprite3D
{
	public enum FacingDirection: ushort 
	{
		Up,
		Right, 
		Down,
		Left
	}

	[Export] float PlayScaleVelocityBase = 1.5f;
	[Export] private string IdleUp = "IdleUp";
	[Export] private string IdleDown = "IdleDown";
	[Export] private string IdleRight = "IdleRight";

	[Export] private string WalkUp = "WalkUp";
	[Export] private string WalkDown = "WalkDown";
	[Export] private string WalkRight = "WalkRight";

	public FacingDirection facing = FacingDirection.Down;

	private bool DirDirty = false;
	private bool VelocityDirty = false;
	private float Velocity = 0.0f;
	private string CurrentAnim = "IdleDown";
	private bool Flipped = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	public void SetVelocity(float NewVelocity)
	{
		if(NewVelocity != Velocity)
		{
			VelocityDirty = true;
		}

		Velocity = NewVelocity;
	}
	
	public void SetFacingDir(FacingDirection dir)
	{
		if(facing == dir)
		{
			return;
		}

		facing = dir;
	}

	public void CommitNewAnimation()
	{
		Play(CurrentAnim);
		if(Velocity > 0.01f)
		{
			SpeedScale = Velocity / PlayScaleVelocityBase;
		}
		else 
		{
			SpeedScale = 1.0f;
		}
		FlipH = Flipped;
	}

	public void EvaluateNewAnimationParameters()
	{
		if(Velocity > 0)
		{
			if(facing == FacingDirection.Up )
			{
				CurrentAnim = WalkUp;
			}

			if(facing == FacingDirection.Down )
			{
				CurrentAnim = WalkDown;
			}

			if(facing == FacingDirection.Right  || facing == FacingDirection.Left)
			{
				CurrentAnim = WalkRight;
			}
		}
		else
		{
			if(facing == FacingDirection.Up )
			{
				CurrentAnim = IdleUp;
			}

			if(facing == FacingDirection.Down )
			{
				CurrentAnim = IdleDown;
			}

			if(facing == FacingDirection.Right  || facing == FacingDirection.Left)
			{
				CurrentAnim = IdleRight;
			}
		}

		if(facing == FacingDirection.Left )
		{
			Flipped = true;
		}
		else 
		{
			Flipped = false;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(DirDirty || VelocityDirty)
		{
			EvaluateNewAnimationParameters();
			CommitNewAnimation();
		}
		DirDirty = false;
	}
}
