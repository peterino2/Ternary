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

	static Rid ShapeRid;
	static bool ShapeRidReady = false;

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

		if(!ShapeRidReady)
		{
			ShapeRid = PhysicsServer3D.SphereShapeCreate();
			PhysicsServer3D.ShapeSetData(ShapeRid, Spawner.BallRadius);
			ShapeRidReady = true;
		}
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
		CheckCollisions(Direction * (float) delta * Speed);
	}

	public override void _ExitTree()
	{
		SpawnOwner.RemoveProjectile(PredictionKey);
	}

	void CheckCollisions(Vector3 DeltaV) 
	{
		var SpaceState = GetWorld3D().DirectSpaceState;
		var Parameters = new PhysicsShapeQueryParameters3D();
		
		Parameters.ShapeRid = ShapeRid;
		var StartTransform = new Transform3D( new Basis(1,0,0,0,1,0,0,0,1), Position + new Vector3(0, HurtRadius + 0.1f, 0));
		Parameters.Transform = StartTransform;
		Parameters.Motion = DeltaV;
        var PlayerBody = SpawnOwner.GetOwnerBody();

        if(PlayerBody != null)
        {
            Parameters.Exclude.Add(PlayerBody.GetRid());
        }

        var Result = SpaceState.IntersectShape(Parameters);
		foreach(var R in Result)
		{
            var colliderAsCharacterBody = R["collider"].Obj as CharacterBody3D;
            if(colliderAsCharacterBody != null)
            {
                if(colliderAsCharacterBody == PlayerBody)
                {
                    DebugDraw3D.DrawSphere(Position, HurtRadius, Colors.Magenta, 5.0f);
                }
                else 
                {
                    DebugDraw3D.DrawSphere(Position, HurtRadius, Colors.Red, 5.0f);
                }
            }
		}
	}
}
