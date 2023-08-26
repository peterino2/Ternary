using Godot;
using System;

public partial class LocalPlayerSubsystem : Node
{
	// ===== statics =====
	static LocalPlayerSubsystem StaticInstance;

	public static LocalPlayerSubsystem Get()
	{
		return StaticInstance;
	}
	// ===== /statics ====

	
	public Player LocalPlayer;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
		GD.PrintRich("Local Player Subsystem Started");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void RegisterLocalPlayer(Player NewLocalPlayer)
	{
		LocalPlayer = NewLocalPlayer;
	}
}
