using Godot;
using System;

public partial class ProjectileSpawner : Node
{
	public long OwnerId = 0;
	
	public bool IsLocallyControlled = false;
	public Node Base;

	[Export] public float BallRadius = 0.4f;
	[Export] public float BallSpeed = 12.0f;
	[Export] public bool ShowDebug = false;
	[Export] public PackedScene ProjectilePrefab;

	Godot.Collections.Dictionary<int, Projectile> Projectiles = new Godot.Collections.Dictionary<int, Projectile>();

	public void SetOwner(long NewOwner)
	{
		if(GameSession.Get().PeerId == OwnerId)
		{
			IsLocallyControlled = true;
		}
		OwnerId = NewOwner;
	}

	public void OnNetTick(long CommandFrame, double Delta)
	{
	}

	public void OnSyncFrame(long CommandFrame, double Delta)
	{
	}

	public Projectile SpawnProjectileLocal(Vector3 FirePoint, Vector3 Direction, int Pk) 
	{
		var LevelNode = GameState.Get().LevelNode;
		
		var NewProjectile = ProjectilePrefab.Instantiate() as Projectile;
		
		NewProjectile.Speed = BallSpeed;
		NewProjectile.HurtRadius = BallRadius;
		NewProjectile.Position = FirePoint;
		NewProjectile.Init(this, Pk, Direction);
		Projectiles[Pk] = NewProjectile;
	

		LevelNode.AddChild(NewProjectile);
		return Projectiles[Pk];
	}

	public void FireProjectile(Vector3 FirePoint, Vector3 Direction, int PredictionKey)
	{
		DrawDebugDirection(FirePoint, Direction);
		var TimeStamp = GameNetEngine.Get().GetTime();

		// 1. submit the fire input to the server
		RpcId(1, nameof(SubmitProjectileFireToServer), new Variant[] {
			FirePoint, Direction, TimeStamp, PredictionKey
		});

		// 2. spawn the locally predicted projectile.
		SpawnProjectileLocal(FirePoint, Direction, PredictionKey);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void InvalidateProjectile(int Pk, bool InvalidateThrow) 
	{
		// If InvalidateThrow is set, then the throw itself never happened.

		NU.Warning("Server Invalidated our projectile launch." + Pk.ToString());

		if(!Projectiles.ContainsKey(Pk))
		{
			NU.Warning("Invalidation issued for PK: " + Pk.ToString() + " But we don't own it");
		}

		var ProjectileToInvalidate = Projectiles[Pk];
		ProjectileToInvalidate.QueueFree();

		RemoveProjectile(Pk);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void BroadCastProjectileSpawn(Vector3 FirePoint, Vector3 Direction, double TimeStamp, int PredictionKey)
	{
		double TimeRecieved = GameNetEngine.Get().GetTime();
		double TimeDelta = (TimeRecieved - TimeStamp);

		var Projectile = SpawnProjectileLocal(FirePoint, Direction, PredictionKey);
		Projectile.Advance(TimeDelta); 
	}

	void DrawDebugDirection(Vector3 Start, Vector3 Direction)
	{
		if(ShowDebug)
		{
			for(int i = 0; i < 12; i += 1)
			{
				DebugDraw3D.DrawSphere(Start + Direction * i * (0.3f), BallRadius, Colors.Orange.Lerp(Colors.Green, (float) i / 12), 2.0f);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void SubmitProjectileFireToServer(Vector3 FirePoint, Vector3 Direction, double TimeStamp, int PredictionKey)
	{
		// used to predict where the object actually is on the client's side
		var TimeRecieved = GameNetEngine.Get().GetTime();
		double TimeDelta = (TimeRecieved - TimeStamp);

		// todo: validation
		// Player's reported position must be within leniency bounds
		// Player must have a ball in hand
		
		var Projectile = SpawnProjectileLocal(FirePoint, Direction, PredictionKey);
		Projectile.Advance(TimeDelta); 
		
		if(ShowDebug)
			NU.Ok("Projectile fire submitted. advance: " + TimeDelta.ToString() + " theirs: " + TimeStamp + " ours: " + TimeRecieved);
		DrawDebugDirection(FirePoint, Direction);

		Rpc(nameof(BroadCastProjectileSpawn), new Variant[] {FirePoint, Direction, TimeStamp, PredictionKey});
	}

	public void SetBase(Node NewBase)
	{
		Base = NewBase;
	}

	public void SetupNetTickables()
	{
		if(OwnerId == GameSession.Get().PeerId)
		{
			if(GameSession.Get().IsServer())
			{
				NU.Error("Bro, we are dedicated server only, owner is also a server.");
			}
			NU.Ok("Movement component setup to send.");
			GameNetEngine.Get().OnNetTick += OnNetTick;
			IsLocallyControlled = true;
		}

		if(GameSession.Get().IsServer())
		{
			NU.Ok("Movement component setup, server side broadcast");
			GameNetEngine.Get().OnSyncFrame += OnSyncFrame;
		}
	}

	public void ShutdownNetTickables()
	{
	}
	
	public void RemoveProjectile(int PredictionKey)
	{
		if(ShowDebug && Projectiles.ContainsKey(PredictionKey))
		{
			NU.Ok("Removing projectile with prediction key" + PredictionKey);
		}

		Projectiles.Remove(PredictionKey);
	}
}

