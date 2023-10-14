using Godot;
using System;

public partial class VfxDeath : Node3D
{
	double DeathTime = 4.0;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		DeathTime -= delta;
		if(DeathTime <0)
		{
			QueueFree();
		}
	}
}
