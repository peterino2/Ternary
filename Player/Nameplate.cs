using Godot;
using System;

public partial class Nameplate : Label3D
{
	/*
	 * How do we get some stuff going with respect to player names,
	 *
	 * When a player connects, it announces to the server its' name as part 
	 * of the validation.
	 *
	 * Server broadcasts the new player to all clients along with its' name
	 * */
	[Export] private Player Player;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Text = GameSession.Get().NamesById[Player.OwnerId];
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnNameSet(string name)
	{
	}
}
