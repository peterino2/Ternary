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

    public Vector3 Team1SpawnArea;
    public Vector3 Team2SpawnArea;

    public List<WorldBall> WorldBalls = new List<WorldBall>();

	// used by the server for validation
	public Dictionary<long, Player> AvatarSpawnedServer = new Dictionary<long, Player>();
	public Dictionary<long, Player> AvatarSpawnedLocal = new Dictionary<long, Player>();

    private List<Player> Team1PlayersDead = new List<Player>();
    private List<Player> Team2PlayersDead = new List<Player>();

    private List<Player> Team1Players = new List<Player>();
    private List<Player> Team2Players = new List<Player>();

    public void RegisterSpawnArea(int TeamId, Vector3 SpawnArea)
    {
        if(TeamId == 0)
        {
            NU.Ok("Team 1 Spawn Area registered at " + SpawnArea.ToString());
            Team1SpawnArea = SpawnArea;
        }
        else 
        {
            NU.Ok("Team 2 Spawn Area registered at " + SpawnArea.ToString());
            Team2SpawnArea = SpawnArea;
        }
    }

    public Vector3 FindPlayerSpawn(int Team)
    {
        var offset = new Vector3((SpawnRandom.NextSingle() - 0.5f) * 6f,0, (SpawnRandom.NextSingle() - 0.5f) * 12.0f);
        if(Team == 0)
        {
            return Team1SpawnArea + offset;
        }
        else 
        {
            return Team2SpawnArea + offset;
        }
    }

    public void KillPlayer(Player DeadPlayer)
    {
        if(!GameSession.Get().IsServer())
        {
            NU.Error("GameState kill function called from somewhere that isnt the server.");
            return;
        }

        DeadPlayer.KillMeServer();
        if(DeadPlayer.TeamId == 0)
        {
            Team1PlayersDead.Add(DeadPlayer);
            if(Team1PlayersDead.Count == Team1Players.Count)
            {
                WinGame(1); // team 2 wins!
            }
        }
        else 
        {
            Team2PlayersDead.Add(DeadPlayer);
            if(Team2PlayersDead.Count == Team2Players.Count)
            {
                WinGame(0); // team 2 wins!
            }
        }
    }

    public void ReviveNextPlayer(int TeamId)
    {
        NU.Ok("Team reviving player at: " + TeamId);

        var RevivedPlayer = Team1PlayersDead[0];
        if(TeamId == 0)
        {
            Team1PlayersDead.RemoveAt(0);
        }
        else 
        {
            RevivedPlayer = Team2PlayersDead[0];
            Team2PlayersDead.RemoveAt(0);
        }

        RevivedPlayer.ReviveMeServer(FindPlayerSpawn(TeamId));
    }

	public override void _Ready()
	{
		NU.Ok("GameState created");
		StaticInstance = this;
		LevelNode = GetNode<Node3D>("/root/Session");
		ReplicatedLevelNode = GetNode<Node>("/root/Session");
	}

    public double ResetCountdown = 0;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if(GameTime > 0)
        {
            GameTime -= delta;
            if(GameTime < 0)
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
        if(Team == 0)
        {
            Team1Players.Add(newPlayer);
        }
        else 
        {
            Team2Players.Add(newPlayer);
        }

        newPlayer.Position = FindPlayerSpawn(newPlayer.TeamId);
        newPlayer.Mover.OverridePosition(newPlayer.Position);
	}

	public void RemovePlayer(long Id)
	{
		AvatarSpawnedServer[Id].ShutDownNet();
		AvatarSpawnedServer[Id].QueueFree();
		AvatarSpawnedServer.Remove(Id);
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

        // revive all players, call overridePoisiton on all of them with new spawn positions.
        foreach(KeyValuePair<long, Player> pair in AvatarSpawnedServer)
        {
            Player player = pair.Value;
            player.ReviveMeServer(FindPlayerSpawn(player.TeamId));
        }
        Team1PlayersDead.Clear();
        Team2PlayersDead.Clear();
        
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

        int PlayerCountTeam1 = Team1Players.Count - Team1PlayersDead.Count;
        int PlayerCountTeam2 = Team2Players.Count - Team2PlayersDead.Count;

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
}
