using Godot;
using System;

public partial class GameLevel : Node3D
{
	// ==== static ====
	public static GameLevel StaticInstance;
	public GameLevel Get()
	{
		return StaticInstance;
	}

	public Node3D GetEntitiesRoot()
	{
		return StaticInstance.EntitiesNode;
	}
	// ==== /static ====
	
	[Export] public Node3D EntitiesNode;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if(StaticInstance == null)
			StaticInstance = this;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
