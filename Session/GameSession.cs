using Godot;
using System;
using System.Text;
using Godot.Collections;
using System.Collections.Generic;

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

	public long PeerId = 0;
	public string HostName = "";
	public int HostPort = 0;
	public string PlayerName = "ShadowRealmJimbo";

    public bool LoginVerifiedClient = false;

	public Godot.Collections.Dictionary<string, long> PlayerIDs = new Godot.Collections.Dictionary<string, long>();

	private byte[] giga = new byte[512];
	bool once = true;

	class DisconnectRequest {
		public long PeerToDisconnect = 0;
		public double TimeOut = 0;
		
		public DisconnectRequest(long PeerId)
		{
			PeerToDisconnect = PeerId;
			TimeOut = 1.0;
		}
	}

	private List<DisconnectRequest> PendingDisconnections = new List<DisconnectRequest>();

	private void TickDisconnects(double Delta)
	{
		for(int i = 0; i < PendingDisconnections.Count; i += 1)
		{
			PendingDisconnections[i].TimeOut -= Delta;
			if(PendingDisconnections[i].TimeOut < 0)
			{
				int PeerToRemove = (int) PendingDisconnections[i].PeerToDisconnect;
				UI_ServerAdmin.Get().RemovePlayer(PeerToRemove);
				Multiplayer.MultiplayerPeer.DisconnectPeer(PeerToRemove);
				PendingDisconnections[i] = PendingDisconnections[PendingDisconnections.Count - 1];
				PendingDisconnections.RemoveAt(PendingDisconnections.Count - 1);

			}
		}
	}

	public override void _Ready()
	{
		StaticInstance = this;
		MaybeTerminatePeer();
		GD.PrintRich($"[color=green] Session created.");
		Multiplayer.ConnectedToServer += PeerConnectedClient;
		Multiplayer.ServerDisconnected += ServerDisconnected;

		Multiplayer.PeerConnected += PeerConnectedServer;
	}

	public void PeerConnectedServer(long Id)
	{
        // I'm not sure if the server gets one itself.
		if(Id == 1 || PeerId != 1)
			return;

		UI_ServerAdmin.Get().AddPlayerToList("<Unknown>", Id);
	}

	public double HeartBeat = 1.0;

	public override void _Process(double Delta)
	{
		if(PeerId != 1)
			return;
			
		HeartBeat -= Delta;
		if(HeartBeat < 0 && once)
		{
			once = false;
			HeartBeat = 1.0;
			GD.Print("Server sending heartbeat");
			Rpc("SendSessionInformation", new Variant[]{ 
				giga
			}) ;
		}

		TickDisconnects(Delta);
	}

	private void MaybeTerminatePeer() 
	{
		if(Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	private void PeerConnectedClient()
	{
		GD.PrintRich($"[color=green] Successfully connected as server peer: " + Multiplayer.GetUniqueId().ToString());
		PeerId = Multiplayer.GetUniqueId();
		Rpc("ValidatePlayerServer", new Variant[] {PlayerName});
	}


	private void ServerDisconnected()
	{
		NU.Error("Disconnected from server");

		MaybeTerminatePeer();
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
		PeerId = Multiplayer.GetUniqueId();
		
		GD.PrintRich($"[color=green] Successfully created server peer");

		HostName = "localhost";
		HostPort = port;
		GD.PrintRich($"[color=green] CreateServer at port" + port.ToString());

		UI_ServerAdmin.Get().SetVisiblityAndProcess(true);
		LoginScreenUI.Get().SetVisiblityAndProcess(false);
	}

	public void ShutdownServer()
	{
		NU.Print("Server shutdown requested");
		MaybeTerminatePeer();
		UI_ServerAdmin.Get().SetVisiblityAndProcess(false);
		LoginScreenUI.Get().SetVisiblityAndProcess(true);
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

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SendSessionInformation(byte[] HeartBeatMessage)
	{
		if(PeerId != 1)
		{
			GD.Print("heartbeat recieved = " + HeartBeatMessage.Length + " >" +Encoding.UTF8.GetString(HeartBeatMessage));
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ValidatePlayerServer(string PlayerName)
	{
		NU.Ok("New Player Name Request: " + PlayerName);
		if(PlayerIDs.ContainsKey(PlayerName))
		{
			NU.Error("Duplicate Player name found, diconnecting player " + Multiplayer.GetRemoteSenderId());
			DisconnectWithMessage(Multiplayer.GetRemoteSenderId(), "Denied, name already in use");
			return;
		}

		Rpc("ValidatePlayerAckClient", new Variant[]{ PlayerName });
		UI_ServerAdmin.Get().SetPlayerName(PlayerName, Multiplayer.GetRemoteSenderId());
		PlayerIDs[PlayerName] =  Multiplayer.GetRemoteSenderId();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ValidatePlayerAckClient(string PlayerName)
	{
		NU.Ok("Login Accepted Name:" + PlayerName);
        LoginVerifiedClient = true;
        LoginScreenUI.Get().UpdateSessionState();
	}

	public void DisconnectWithMessage(long PeerToDisconnect, string Message)
	{
		Rpc("DisconnectClient", new Variant[]{ PeerToDisconnect, Message }) ;
		PendingDisconnections.Add(new DisconnectRequest( PeerToDisconnect));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void DisconnectClient(long PeerToDisconnect, string Message)
	{
		// message wasn't intended for us
		if(PeerToDisconnect != PeerId)
		{
			return;
		}
		NU.Error("Server issued a disconnect with message: " + Message);
		LoginScreenUI.Get().ShowError("Server issued a disconnect with message: " + Message);
	}

}

