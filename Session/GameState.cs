using Godot;
using System;

using System.Net.NetworkInformation;

public partial class GameState : Node
{
	[Export] private int port = 7777;
	[Export] private Node levelNode;
	[Export] private PackedScene levelScene;
	[Export] private int MaxPeers = 32;
	
	public string testString = "nibba";

	private void LogError(string Msg)
	{
		GD.PrintRich($"[color=red] Error: " + Msg);
	}

	private void LogOk(string Msg)
	{
		GD.PrintRich($"[color=Green] Info : " + Msg);
	}

	public override void _Ready()
	{
		MaybeTerminatePeer();
	}

	private void MaybeTerminatePeer() 
	{
		if(Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	private void ClientSendSessionLoaded() 
	{
	}


	private void StartServer()
	{
		ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
		Error e = peer.CreateServer(port);
		if(peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			GD.PrintRich($"[color=red] Failed To Start Server");
			return;
		}
		Multiplayer.MultiplayerPeer = peer;
		
		GD.PrintRich($"[color=green] Successfully created server peer");
		StartGame();
	}


	public void ConnectAsClient(string host, int port) 
	{
		if(Multiplayer.MultiplayerPeer != null)
		{
			GD.PrintRich($"[color=red] failed to start client, client already started");
			return;
		}

		ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
		peer.CreateClient(host, port);

		if(peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			LogError("Unable to connect to " + host + ":" + port.ToString());
			return;
		}
	}

	private void StartGame() {
		if(Multiplayer.IsServer() == false) return;
		levelNode.AddChild(levelScene.Instantiate());
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	// some helper RPCs to test that the connection works.
}
