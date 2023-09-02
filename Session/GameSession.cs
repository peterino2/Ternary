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

	// Client/Server session information
	public long PeerId = 0; // 0 for disconnected, 1 for server, Other positive numbers are clients
	public string HostName = ""; // loopback host name.
	public int HostPort = 0; // only valid when acting as server or when connected
	public string PlayerName = "<Unknown>";  // only valid when acknowledged by server.

	// when set to true, this client has been VERIFIED by server ans is good to recieve further comms.
	public bool LoginVerifiedClient = false; 

	// Only set when this is created as the server.
	public bool HasAuthority = false;

	// Only set when the game is created on the server.
	public bool GameStarted = false;

	public Godot.Collections.Dictionary<string, long> IdsByName = new Godot.Collections.Dictionary<string, long>();
	public Godot.Collections.Dictionary<long, string> NamesById = new Godot.Collections.Dictionary<long, string>();
	public Godot.Collections.Dictionary<long, bool> Verification = new Godot.Collections.Dictionary<long, bool>();

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
        Multiplayer.PeerDisconnected += PeerDisconnectedServer;
	}

    public void PeerDisconnectedServer(long Id)
    {
        var PlayerToDc = NamesById[Id];
        NU.Warning("Player Disconnected : " + PlayerToDc + "[" + Id.ToString() + "]");
        NamesById.Remove(Id);
        IdsByName.Remove(PlayerToDc);

        if(PeerId == 1)
        {
            UI_ServerAdmin.Get().RemovePlayer(Id);
            GameState.Get().RemovePlayer(Id);
        }
    }

	public void PeerConnectedServer(long Id)
	{
		// I'm not sure if the server gets one itself.
		if(Id == 1 || PeerId != 1)
			return;

		UI_ServerAdmin.Get().AddPlayerToList("<Unknown>", Id);
		Verification[Id] = false;
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
		RpcId(1, nameof(ValidatePlayerServer), new Variant[] {PlayerName});
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
		HasAuthority = true;
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
			GD.Print("heartbeat recieved = " + HeartBeatMessage.Length + " >" + Encoding.UTF8.GetString(HeartBeatMessage));
		}
	}

    private void BroadCastPlayerList()
    {
        foreach(KeyValuePair<long, string> pair in NamesById)
        {
		    Rpc(nameof(RecievePlayerInfoClient), new Variant[]{ pair.Key, pair.Value });
        }
    }

	// Server validation of the new incomming player.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void ValidatePlayerServer(string PlayerName)
	{
		if(PeerId != 1)
		{
			return;
		}

		NU.Ok("New Player Name Request: " + PlayerName);
		if(IdsByName.ContainsKey(PlayerName))
		{
			NU.Error("Duplicate Player name found, diconnecting player " + Multiplayer.GetRemoteSenderId());
			DisconnectWithMessage(Multiplayer.GetRemoteSenderId(), "Denied, name already in use");
			return;
		}

		UI_ServerAdmin.Get().SetPlayerName(PlayerName, Multiplayer.GetRemoteSenderId());
		IdsByName[PlayerName] =  Multiplayer.GetRemoteSenderId();
        NamesById[Multiplayer.GetRemoteSenderId()] = PlayerName;
		Verification[Multiplayer.GetRemoteSenderId()] = true;

        BroadCastPlayerList();

        var PlayerCountToStart = SessionConfigs.Get().SettingsConfig.AutoStartGamePlayerCount;
        if(PlayerCountToStart > 0 )
        {
            if(IdsByName.Count >= PlayerCountToStart)
            {
                UI_ServerAdmin.Get().StartGame();
            }
        }
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RecievePlayerInfoClient(long ClientId, string PlayerName)
	{
        if(ClientId == PeerId)
        {
            LoginVerifiedClient = true;
            LoginScreenUI.Get().UpdateSessionState();
        }

        if(!NamesById.ContainsKey(ClientId))
        {
		    NU.Ok("NewPlayerRecieved: " + PlayerName + $"[{ClientId.ToString()}]");
        }

		IdsByName[PlayerName] = ClientId;
        NamesById[ClientId] = PlayerName;
	}


	// ===== Disconnections  =========
	public void DisconnectWithMessage(long PeerToDisconnect, string Message)
	{
		Rpc("OnDisconnectClient", new Variant[]{ PeerToDisconnect, Message }) ;
		PendingDisconnections.Add(new DisconnectRequest( PeerToDisconnect));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void OnDisconnectClient(long PeerToDisconnect, string Message)
	{
		// message wasn't intended for us
		if(PeerToDisconnect != PeerId)
		{
			return;
		}
		NU.Error("Server issued a disconnect with message: " + Message);
		LoginScreenUI.Get().ShowError("Server issued a disconnect with message: " + Message);
	}

	// =========================
	// START THE GAME 
	// =========================

	// Sends the signal to all clients that the game is about to begin
	public void StartGame(Node3D LevelNode, PackedScene LevelScene)
	{
		// assert(PeerId == 1);
		// assert(GameStarted = false);
        if(GameStarted != false)
        {
			NU.Warning("Start game signal ignored, game already started");
            return;
        }
		if(LevelNode == null)
		{
			NU.Error("Invalid node passed in to StartGame()");
			return;
		}

		NU.Print("Server Starting Game...");
		GameState.Get().LevelNode = LevelNode;
		GameState.Get().LevelScene = LevelScene;
		GameStarted = true;
		GameState.Get().PrepareGame();
	}

    public void RequestGameStartFromClient() 
    {
        RpcId(1, nameof(RecievedGameStartRequestServer), new Variant[] {});
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RecievedGameStartRequestServer() 
    {
        if(PeerId != 1)
        {
            NU.Error("GameSession::RecievedGameStartRequestServer got called on non-server???");
            return;
        }
        // TODO add an admin/Root system.
        // ALSO TODO: Dont store games session stuff on the UI_ServerAdmin... maybe?
        UI_ServerAdmin.Get().StartGame();
    }

    public bool IsServer() 
    {
        return PeerId == 1;
    }
}

