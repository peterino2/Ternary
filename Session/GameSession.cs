using Godot;
using System;

using System.Net.NetworkInformation;


public partial class GameSession: Node
{
	// ==== statics ====
	public static GameSession StaticInstance;

	public static GameSession Get() { 
		return StaticInstance;
	}

	// === /statics ====
	
	// used to determine the maximum number of peers
	// that can connect to a server at a time.
	// server itself is considered one when not in listen mode.
	// We do not support listen server, dedicated only
	[Export] private int MaxPeers = 32 + 1;

	public string HostName = "";
	public int HostPort = 0;

	public override void _Ready()
	{
		StaticInstance = this;
		MaybeTerminatePeer();
		GD.PrintRich($"[color=green] Session created.");
		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ServerDisconnected += ServerDisconnected;
	}

	private void MaybeTerminatePeer() 
	{
		if(Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	private void ConnectedToServer()
	{
		GD.PrintRich($"[color=green] Successfully connected as server peer: " + Multiplayer.GetUniqueId().ToString());
	}

	private void ServerDisconnected()
	{
		GD.PrintRich($"[color=yellow] DisConnected from server");
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
			GD.PrintRich($"[color=red] Failed to create client for " + host + ":" + port.ToString());
			return;
		}

		GD.PrintRich($"[color=green] Created Client connection to " + host + ":" + port.ToString());
		Multiplayer.MultiplayerPeer = peer;

		HostName = host;
		HostPort = port;
	}
}

