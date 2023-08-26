using Godot;
using System;

public partial class SC_Session : Node3D
{

	[Export] private Node levelNode;
	[Export] private PackedScene levelScene;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
