using Godot;
using System;
using System.Collections.Generic;


// Top level Class for the top level state of the game and the game.
public partial class GameState: Node
{
	// ==== statics ====
	public static GameState StaticInstance;
	
	public static GameState Get() 
	{
		return StaticInstance;
	}
	// ==== /statics ====
	
	public Node LevelNode;
	public Node ReplicatedLevelNode;
	public PackedScene LevelScene;

	public GameLevel Level;
	public bool LevelReady = false; // called when GameLevel sets itself to be true
	public bool AvatarSpawned = false; // used by client to determine if we have issued a spawn request yet

	public float SpawnRadius = 1.0f;

	Random SpawnRandom = new Random();

	// Time for the game session (in seconds)
	public double GameTime = 0.0;
	public double GameResetTime = 180.0;

	public List<Vector3> SpawnAreas = new List<Vector3>();

	public List<WorldBall> WorldBalls = new List<WorldBall>();

	// used by the server for validation
	public Dictionary<long, Player> AvatarSpawnedServer = new Dictionary<long, Player>(); // empty on local
	public Dictionary<long, Player> AvatarSpawnedLocal = new Dictionary<long, Player>(); // available to all

	private List<List<Player>> PlayersDead = new List<List<Player>>();
	private List<List<Player>> TeamPlayers = new List<List<Player>>();

	public override void _Ready()
	{
		NU.Ok("GameState created");
		StaticInstance = this;
		LevelNode = GetNode<Node3D>("/root/Session");
		ReplicatedLevelNode = GetNode<Node>("/root/Session");
		PlayersDead.Add(new List<Player>());
		PlayersDead.Add(new List<Player>());

		TeamPlayers.Add(new List<Player>());
		TeamPlayers.Add(new List<Player>());

		SpawnAreas.Add(new Vector3());
		SpawnAreas.Add(new Vector3());
	}

	public void RegisterSpawnArea(int TeamId, Vector3 SpawnArea)
	{
		NU.Ok("Team " + TeamId.ToString() +" Spawn Area registered at " + SpawnArea.ToString());
		SpawnAreas[TeamId - 1] = SpawnArea;
	}

	public Vector3 FindPlayerSpawn(int Team)
	{
		var offset = new Vector3((SpawnRandom.NextSingle() - 0.5f) * 6f,0, (SpawnRandom.NextSingle() - 0.5f) * 12.0f);
		return SpawnAreas[Team - 1];
	}

	public void KillPlayer_s(Player DeadPlayer, bool AddToDeadList=false)
	{
		if(!GameSession.Get().IsServer())
		{
			NU.Error("GameState kill function called from somewhere that isnt the server.");
			return;
		}

		DeadPlayer.KillMeServer();
		var DeadTeam = DeadPlayer.TeamId;
		PlayersDead[DeadTeam - 1].Add(DeadPlayer);
		if(PlayersDead[DeadTeam - 1].Count == TeamPlayers[DeadTeam - 1].Count) 
		{
			if(DeadTeam == 1)
			{
				WinGame(2);
			}
			if(DeadTeam == 2)
			{
				WinGame(1);
			}
		}
	}

	public void ReviveNextPlayer(int TeamId)
	{
		NU.Ok("Team reviving player at: " + TeamId);

		var RevivedPlayer = PlayersDead[TeamId - 1][0];
		PlayersDead[TeamId - 1].RemoveAt(0);
		RevivedPlayer.ReviveMeServer(FindPlayerSpawn(TeamId));
	}


	public double ResetCountdown = 0;


    bool LastOpenScoreState = false;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if(GameSession.Get().PeerId == 0)
        {
            return;
        }

		if(GameTime > 0)
		{
			GameTime -= delta;
			if(GameTime <= 0)
			{
				StartResetCountdown();
			}
		}
		else if(ResetCountdown > 0 && GameSession.Get().IsServer())
		{
			ResetCountdown -= delta;
			if(ResetCountdown < 0 && GameSession.Get().IsServer())
			{
				 ResetGameServer();
			}
		}

		TickServerState(delta);

        if(!GameSession.Get().IsServer())
        {
			var OpenMenu = Input.GetActionStrength("OpenScore");
			if(OpenMenu > 0.5 && AvatarSpawned == true && LocalLobbyControls.Get().Visible == false && LastOpenScoreState == false)
            {
                LastOpenScoreState = true;
                LocalLobbyControls.Get().SetVisiblityAndProcess(true);
            }
            else if(OpenMenu > 0.5 && AvatarSpawned == true && LocalLobbyControls.Get().Visible == true && LastOpenScoreState == false)
            {
                LastOpenScoreState = true;
                LocalLobbyControls.Get().SetVisiblityAndProcess(false);
            }

            if(OpenMenu < 0.5)
            {
                LastOpenScoreState = false;
            }
        }
	}

	void StartResetCountdown()
	{
		ResetCountdown = 5.0;
	}

	public void UpdateLoadingState()
	{
		if(LevelReady && !AvatarSpawned)
		{
			if(GameSession.Get().PeerId != 1)
			{
				RpcId(1, nameof(SpawnAvatar), new Variant[]{ GameSession.Get().PlayerName });
				AvatarSpawned = true;
			}
		}
	}

	public void PrepareGame()
	{
		NU.Ok("preparing game");
		LevelNode.AddChild(LevelScene.Instantiate());
		GameTime = GameResetTime;
		Rpc(nameof(GameStartMulticast), new Variant[]{});
		GameSession.Get().OnTeamChanged += OnPlayerTeamChanged_s;
	}

	// GameSession should contain the actual team selection.
	public void OnPlayerTeamChanged_s(long playerId, int newTeam)
	{
		var player = AvatarSpawnedServer[playerId];
		KillPlayer_s(player, false);
	}

	private int count = 0;

	public delegate void GameStartDelegate();
	public event GameStartDelegate OnGameStart;
	public event GameStartDelegate OnPlayerCountChanged;
	public delegate void GameWinDelegate(int Team);
	public event GameWinDelegate OnGameWin;

	public delegate void KilledDelegate(Player Killer, Player Killed);
	public event KilledDelegate OnKill;


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void GameStartMulticast()
	{
		GameTime = GameResetTime;
		OnGameStart?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void SpawnAvatar(string PlayerName)
	{
		if(GameSession.Get().PeerId != 1)
		{
			return;
		}

		if(AvatarSpawnedServer.ContainsKey( Multiplayer.GetRemoteSenderId()))
		{
			NU.Error("Avatar already spawned for player " + PlayerName + "[" + Multiplayer.GetRemoteSenderId() + "]");
			return;
		}

		NU.Ok("Requesting Avatar Spawn from client for Player: " + PlayerName);
		
		var newPlayer = GameLevel.Get().AvatarScene.Instantiate() as Player;
		newPlayer.Name = "Player" + Multiplayer.GetRemoteSenderId().ToString();
		newPlayer.SetOwnerServer(Multiplayer.GetRemoteSenderId());
		newPlayer.SetTeam(GameSession.Get().TeamIds[Multiplayer.GetRemoteSenderId()]);
		newPlayer.DebugName = PlayerName;

		GameLevel.Get().GetEntitiesRoot().AddChild(newPlayer);

		AvatarSpawnedServer[Multiplayer.GetRemoteSenderId()] = newPlayer;

		var Team = newPlayer.TeamId;
		TeamPlayers[Team - 1].Add(newPlayer);

		newPlayer.Position = FindPlayerSpawn(newPlayer.TeamId);
		newPlayer.Mover.OverridePosition(newPlayer.Position);
	}

	public void RemovePlayer(long Id)
	{
        if(AvatarSpawnedServer.ContainsKey(Id))
        {
            KillPlayer_s(AvatarSpawnedServer[Id]);
            AvatarSpawnedServer[Id].ShutDownNet();
            AvatarSpawnedServer[Id].QueueFree();
            AvatarSpawnedServer.Remove(Id);
        }
	}

	public void WinGame(int Team)
	{
		NU.Ok("Team: " + (Team + 1).ToString() + " Won!");
		GameTime = 2.0;
		Rpc(nameof(GameWinBroadcast), new Variant[]{Team});
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void GameWinBroadcast(int Team)
	{
		GameTime = 2.0;
		OnGameWin?.Invoke(Team);
	}

	public void ResetGame()
	{
		RpcId(1, nameof(DebugResetGameServer), new Variant[]{});
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void DebugResetGameServer()
	{
		if(!GameSession.Get().IsServer())
		{
			return;
		}

		ResetGameServer();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void ResetGameMulticast()
	{
		GameTime = GameResetTime;
		OnGameStart?.Invoke();
	}

	public void ResetGameServer()
	{
		foreach(var WorldBall in WorldBalls)
		{
			// randomly renable all worldballs along the x = 0 line z = 
			WorldBall.ReEnableAt(new Vector3(0, 1.0f, (SpawnRandom.NextSingle() - 0.5f) * 2.0f * 10.0f ));
		}

		for(int i = 0; i < TeamPlayers.Count; i += 1)
		{
			TeamPlayers[i].Clear();
            PlayersDead[i].Clear();
		}
		
		// revive all players, call overridePoisiton on all of them with new spawn positions.
		foreach(KeyValuePair<long, Player> pair in AvatarSpawnedServer)
		{
			Player player = pair.Value;
            player.TeamId = GameSession.Get().TeamIds[pair.Key];
			player.ReviveMeServer(FindPlayerSpawn(player.TeamId));
            
            TeamPlayers[player.TeamId - 1].Add(player);
		}

		Rpc(nameof(ResetGameMulticast), new Variant[]{});
	}

	public int SyncedPlayerCountTeam1 = 0;
	public int SyncedPlayerCountTeam2 = 0;

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void BroadcastGameState(int PlayerCountTeam1, int PlayerCountTeam2)
	{
		SyncedPlayerCountTeam1 = PlayerCountTeam1;
		SyncedPlayerCountTeam2 = PlayerCountTeam2;
		OnPlayerCountChanged?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
	public void BroadcastKill(long KillerId, long KilledId)
	{
		OnKill?.Invoke(AvatarSpawnedLocal[KillerId], AvatarSpawnedLocal[KilledId]);
	}

	public void TickServerState(double delta)
	{
		if(!GameSession.Get().IsServer())
		{
			return;
		}

		int PlayerCountTeam1 = TeamPlayers[0].Count - PlayersDead[0].Count;
		int PlayerCountTeam2 = TeamPlayers[1].Count - PlayersDead[1].Count;

		if(PlayerCountTeam1 != SyncedPlayerCountTeam1 || PlayerCountTeam2 != SyncedPlayerCountTeam2)
		{
			SyncedPlayerCountTeam2 = PlayerCountTeam2;
			SyncedPlayerCountTeam1 = PlayerCountTeam1;

			Rpc(nameof(BroadcastGameState), new Variant[]{PlayerCountTeam1, PlayerCountTeam2});
		}
	}

	public void ServerBroadcastKill(long KillerId, long KilledId)
	{
		Rpc(nameof(BroadcastKill), new Variant[]{
			KillerId, KilledId
		});
	}

    public void NewPlayerJoined(long PeerId, string PlayerName, int team)
    {
    }
}


