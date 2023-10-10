using Godot;
using System;

public partial class Projectile : Node3D
{
	public long OwnerId = 0;
	public ProjectileSpawner SpawnOwner;
	public bool IsLocallyControlled = false;
	public int PredictionKey = 0;

	public float Speed = 12.0f;
	public float HurtRadius = 0.4f;
	public double LifeTime = 2.0f;

	public Vector3 Direction = new Vector3(0,0,0);

	public void Init(ProjectileSpawner Spawner, int pk, Vector3 InitDirection)
	{
		OwnerId = Spawner.OwnerId;
		SpawnOwner = Spawner;
		if(OwnerId == GameSession.Get().PeerId)
		{
			IsLocallyControlled = true;
		}

		Direction = InitDirection;

		PredictionKey = pk;
	}

	public override void _Process(double delta)
	{
		DebugDraw3D.DrawSphere(Position, HurtRadius, Colors.Orange, 0.03f);
		Advance(delta);

		if(LifeTime < 0)
		{
			QueueFree();
		}
	}

	// override me for more complex physics
	public virtual void Advance(double delta)
	{
		LifeTime -= delta;
		Position += Direction * (float) delta * Speed;
	}
}
