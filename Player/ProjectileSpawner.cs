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

	public void FireProjectile(Vector3 FirePoint, Vector3 Direction)
	{
		DrawDebugDirection(FirePoint, Direction);

		RpcId(1, nameof(SubmitProjectileFireToServer), new Variant[] {
			FirePoint, Direction, Time.GetTicksUsec()
		});
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void SubmitProjectileFireToServer(Vector3 FirePoint, Vector3 Direction, int Microsec)
	{
		NU.Ok("Projectile fire submitted.");
		DrawDebugDirection(FirePoint, Direction);
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

}
