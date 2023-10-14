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

    public Vector3 Team1SpawnArea;
    public Vector3 Team2SpawnArea;

	// used by the server for validation
	public Dictionary<long, Player> AvatarSpawnedServer = new Dictionary<long, Player>();

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
        var offset = new Vector3(SpawnRandom.NextSingle() * 6f,0, SpawnRandom.NextSingle() * 3.0f);
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

        RevivedPlayer.ReviveMeServer(new Vector3(0,0,0));
    }

	public override void _Ready()
	{
		NU.Ok("GameState created");
		StaticInstance = this;
		LevelNode = GetNode<Node3D>("/root/Session");
		ReplicatedLevelNode = GetNode<Node>("/root/Session");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
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

        Rpc(nameof(GameStartMulticast), new Variant[]{});
	}

	private int count = 0;

    public delegate void GameStartDelegate();
    public event GameStartDelegate OnGameStart;

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] 
    public void GameStartMulticast()
    {
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
    }

}
