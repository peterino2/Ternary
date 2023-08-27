using Godot;
using System;

using System.Net.NetworkInformation;

// Top level Class for the top level state of the game and the game.
public partial class GameState: Node
{
	// ==== statics ====
	public static GameState State;
	
	public static GameState Get() 
	{
		return State;
	}
	// ==== /statics ====
	
	[Export] public Node levelNode;
	[Export] public PackedScene levelScene;

	public override void _Ready()
	{
		NU.Ok("GameState created");
		State = this;
	}

	public void PrepareGameClient() 
	{
		levelNode.AddChild(levelScene.Instantiate());
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void BeginLobbyModeServer() 
	{
	}
}
