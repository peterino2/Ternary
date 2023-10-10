using Godot;
using System;

public partial class PlayerCamera : Node3D
{
	public Node3D ViewTarget;
	
	[Export] bool Snap = false;
	[Export] bool AutoFindPlayer = true;
	[Export] public Camera3D Camera;

	static PlayerCamera StaticInstance;

	float RotationAngle = 0;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
	}

	public static PlayerCamera Get() 
	{
		return StaticInstance;
	}

	public void SetViewTarget(Node3D NewViewTarget)
	{
		ViewTarget = NewViewTarget;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(ViewTarget == null)
		{
			if(LocalPlayerSubsystem.Get().LocalPlayer != null)
			{
				ViewTarget = LocalPlayerSubsystem.Get().LocalPlayer;
			}
			return;
		}
		if(Snap)
		{
			Position = ViewTarget.Position;
			return;
		}
		var Dir = (ViewTarget.Position - Position).Normalized();
		var Len = (ViewTarget.Position - Position).Length();

		float Speed = (float) Len / 0.5f * 2f;

		Vector3 Velocity = new Vector3(
				Dir.X * (float)delta * Speed,
				Dir.Y * (float)delta * Speed,
				Dir.Z * (float)delta * Speed
		);

		Position = Position + Velocity;
	}
}
