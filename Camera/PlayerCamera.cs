using Godot;
using System;

public partial class PlayerCamera : Node3D
{
	public Node3D ViewTarget;
	
	[Export] bool Snap = false;
	float RotationAngle = 0;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ViewTarget = GetNode<Node3D>("/root/Main/Player");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
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
