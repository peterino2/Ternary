using Godot;
using System;

using System.Net.NetworkInformation;

public partial class GameState : Node
{
	[Export] public Node levelNode;
	[Export] public PackedScene levelScene;
	[Export] private int MaxPeers = 32;
	
	public static GameState state;

	public string HostName = "";
	public int HostPort = 0;

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
		state = this;
		MaybeTerminatePeer();

		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ServerDisconnected += ServerDisconnected;
	}

	private void ConnectedToServer()
	{
		GD.PrintRich($"[color=green] Successfully connected as server peer: " + Multiplayer.GetUniqueId().ToString());
		SpawnLevel();
	}
	
	private void ServerDisconnected()
	{
		GD.PrintRich($"[color=yellow] DisConnected from server");
	}

	private void MaybeTerminatePeer() 
	{
		if(Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	public void StartServer(int port)
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

		HostName = "localhost";
		HostPort = port;

		GD.PrintRich($"[color=green] CreateServer at port" + port.ToString());

		SpawnLevel();
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
			LogError("Failed to create client for " + host + ":" + port.ToString());
			return;
		}

		GD.PrintRich($"[color=green] Created Client connection to " + host + ":" + port.ToString());
		Multiplayer.MultiplayerPeer = peer;

		HostName = host;
		HostPort = port;
	}

	private void SpawnLevel() {
		levelNode.AddChild(levelScene.Instantiate());
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	// some helper RPCs to test that the connection works.
}
