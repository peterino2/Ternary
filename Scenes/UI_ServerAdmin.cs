using Godot;
using System;
using System.Collections.Generic;

public partial class UI_ServerAdmin : Control
{
	private const bool Debug = true;

	// === statics ===
	static UI_ServerAdmin StaticInstance;
	
	public static UI_ServerAdmin Get()
	{
		return StaticInstance;
	}
	// === statics ===

	[Export] private RichTextLabel StatusLabel;
	[Export] private Button ShutdownServerButton;
	[Export] private Button StartGameServer;
	[Export] private PackedScene PackedScene;

	[Export] private BoxContainer PlayerListBoxContainer;
	[Export] private PackedScene PlayerListEntryScene;

	private List<UI_PlayerListEntry> PlayerListEntries = new List<UI_PlayerListEntry>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
		ShutdownServerButton.ButtonDown += ShutdownServer;
		StartGameServer.ButtonDown += StartGame;
		NU.Print("serverAdmin running");
	}

	public void StartGame() 
	{
		var rootnode = GetNode<Node3D>("/root/Session");
		GameSession.Get().StartGame(rootnode, PackedScene);
	}


	public void SetVisiblityAndProcess(bool NewVisibility)
	{
		Visible = NewVisibility;
		SetProcess(NewVisibility);
	}

	public void ShutdownServer()
	{
		NU.Print("Shutting down");
		GameSession.Get().ShutdownServer();

		for(int i = 0; i < PlayerListEntries.Count; i += 1)
		{
			PlayerListEntries[i].QueueFree();
		}
		PlayerListEntries.Clear();
	}

	double PingTime = 1.0;

	public override void _Process(double Delta)
	{
		PingTime -= Delta;
		if(PingTime < 0 )
		{
			PingTime = 1.0;
			UpdatePing();
		}
	}

	void UpdatePing()
	{
		for(int i = 0; i < PlayerListEntries.Count; i += 1)
		{
			PlayerListEntries[i].UpdatePing();
		}
	}

	public void RemovePlayer(long Id)
	{
		NU.Ok("Removing player from UI: " + Id.ToString());
		for(int i = 0; i < PlayerListEntries.Count; i += 1)
		{
			if(PlayerListEntries[i].Id == Id)
			{
				PlayerListEntries[i].QueueFree();
				PlayerListEntries.RemoveAt(i);
			}
		}
	}

	public void AddPlayerToList(string PlayerName, long Id)
	{
		var NewBox = PlayerListEntryScene.Instantiate() as UI_PlayerListEntry;
		PlayerListEntries.Add(NewBox);
		PlayerListBoxContainer.AddChild(NewBox);
		NewBox.SetId(Id);
		NewBox.SetName(PlayerName);
	}

	public void SetPlayerName(string PlayerName, long Id) 
	{
		for(int i = 0; i < PlayerListEntries.Count; i += 1)
		{
			if(PlayerListEntries[i].Id == Id)
			{
				if(Debug) NU.Ok("Player Name updated Id: " + Id + " name:" + PlayerName);
				PlayerListEntries[i].SetName(PlayerName);
			}
		}
	}
}
