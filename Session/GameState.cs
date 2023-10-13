using Godot;
using System;
using System.Collections.Generic;

using System.Net.NetworkInformation;

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

	// used by the server for validation
	public Dictionary<long, Player> AvatarSpawnedServer = new Dictionary<long, Player>();

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
	}

	private int count = 0;

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
        newPlayer.DebugName = PlayerName;

		GameLevel.Get().GetEntitiesRoot().AddChild(newPlayer);
		AvatarSpawnedServer[Multiplayer.GetRemoteSenderId()] = newPlayer;
	}

	public void RemovePlayer(long Id)
	{
		AvatarSpawnedServer[Id].ShutDownNet();
		AvatarSpawnedServer[Id].QueueFree();
		AvatarSpawnedServer.Remove(Id);
	}

}
