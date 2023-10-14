using Godot;

public partial class Projectile : Node3D
{
	[Export] MeshInstance3D Mesh;
	[Export] VfxFire Fire;
	public long OwnerId = 0;
	public ProjectileSpawner SpawnOwner;
	public int TeamId = 0;
	public bool IsLocallyControlled = false;
	public int PredictionKey = 0;

	public WorldBall WorldBallRef = null;

	public float Speed = 25.0f;
	public float HurtRadius = 0.4f;
	public double LifeTime = 10.0f;
	bool IsDead = false;

	public Vector3 Direction = new Vector3(0, 0, 0);

	static Rid ShapeRid;
	static bool ShapeRidReady = false;

	public void Init(ProjectileSpawner Spawner, int pk, Vector3 InitDirection)
	{
		OwnerId = Spawner.OwnerId;
		SpawnOwner = Spawner;
		TeamId = (SpawnOwner.GetOwnerBody() as Player).TeamId;

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
		//DebugDraw3D.DrawSphere(Position, HurtRadius, Colors.Orange, 0.03f);
		Advance(delta);

		if(LifeTime < 0)
		{
			QueueFree();
		}
	}

	public void FreezeAndKill()
	{
		Fire.StopFlames();
		Mesh.Visible = false;
		IsDead = true;
		LifeTime = 1.0;
		Speed = 0.0f;
	}

	// override me for more complex physics
	public virtual void Advance(double delta)
	{
		LifeTime -= delta;
		if(IsDead)
		{
			return;
		}

		Position += Direction * (float) delta * Speed;
		CheckCollisions(Direction * (float) delta * Speed);
	}

	public override void _ExitTree()
	{
		SpawnOwner.RemoveProjectile(PredictionKey);
	}

	void SignalBounceBack(Vector3 ImpactPoint, Vector3 ImpactNormal)
	{
		NU.Ok("signaled length: " + ImpactNormal.Length());
		if(WorldBallRef != null)
		{
			DebugDraw3D.DrawRay(ImpactPoint, ImpactNormal, 5.0f, Colors.Red, 5.0f);
			WorldBallRef.ReEnableAt(ImpactPoint);
			WorldBallRef.LinearVelocity = new Vector3(0,0,0);
			WorldBallRef.ApplyCentralImpulse(ImpactNormal * 5.0f);
		}
		else 
		{
			NU.Error("Bounce back signaled but no ball referenced?");
		}
	}

	void CheckCollisions(Vector3 DeltaV) 
	{
		var SpaceState = GetWorld3D().DirectSpaceState;
		var Parameters = new PhysicsShapeQueryParameters3D();

		Parameters.ShapeRid = ShapeRid;
		var StartTransform = new Transform3D(
				new Basis(1,0,0,0,1,0,0,0,1), Position 
				+ new Vector3(0, HurtRadius + 0.1f, 0));

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
			// NU.Ok("R: " + R.ToString());
			var colliderAsCharacterBody = R["collider"].Obj as CharacterBody3D;
			if(colliderAsCharacterBody != null)
			{
				if(colliderAsCharacterBody != PlayerBody)
				{
					// DebugDraw3D.DrawSphere(Position, HurtRadius, Colors.Red, 5.0f);

					if(GameSession.Get().IsServer())
					{
						var colliderAsPlayer = colliderAsCharacterBody as Player;

						if(colliderAsPlayer != null)
						{
							var dir = (colliderAsPlayer.GlobalPosition - Position).Normalized();
							if(colliderAsPlayer.IsDead || colliderAsPlayer.IsDodging())
							{
								// nothing happens, hes a ghost
							}
							else if(colliderAsPlayer.CheckCatching(Position))
							{
								NU.Ok("Player caught the ball");
								colliderAsPlayer.PlayerCaughtBallOnServer(WorldBallRef);
								GameState.Get().ReviveNextPlayer(colliderAsPlayer.TeamId);
								colliderAsPlayer.ServerAddImpulse(new Vector2(dir.X, dir.Z) * 6.0f);
								FreezeAndKill();
								break;
							}
							else if (colliderAsPlayer.CheckBlocking(Position))
							{
								SignalBounceBack(Position, -dir);
								colliderAsPlayer.ServerAddImpulse(new Vector2(dir.X, dir.Z) * 6.0f);
								FreezeAndKill();
								break;
							}
							else 
							{
								if(colliderAsPlayer.TeamId != SpawnOwner.PlayerRef.TeamId)
								{
									GameState.Get().KillPlayer(colliderAsPlayer);
                                    GameState.Get().ServerBroadcastKill(OwnerId, colliderAsPlayer.OwnerId);

									colliderAsPlayer.ServerAddImpulse(new Vector2(dir.X, dir.Z) * 6.0f);
									SignalBounceBack(Position, dir);
									FreezeAndKill();
									break;
								}
							}
						}
					}
					else
					{
						var colliderAsPlayer = colliderAsCharacterBody as Player;
						if(colliderAsPlayer != null)
						{
							if(!colliderAsPlayer.IsDead && (colliderAsPlayer.TeamId != TeamId) && !colliderAsPlayer.IsDodging())
							{
								FreezeAndKill();
							}
						}
						else 
						{
							FreezeAndKill();
						}
					}
				}

			}
			else 
			{
				if(R["collider"].Obj as MiddleSplit != null)
				{
				}
				else 
				{
					var ColliderAsStatic = R["collider"].Obj as StaticBody3D;
					if(ColliderAsStatic != null)
					{
						// DebugDraw3D.DrawSphere(Position, HurtRadius, Colors.Red, 5.0f);
						FreezeAndKill();
						if(GameSession.Get().IsServer())
						{
							// sigh here we go again...
							var RayParams = PhysicsRayQueryParameters3D.Create(Position, Position + DeltaV.Normalized() * 5.0f);
							var RayResults = SpaceState.IntersectRay(RayParams);

							var Normal = (ColliderAsStatic.GlobalPosition - Position).Normalized();
							var dir  = new Vector3(0,1,0);
							if(RayResults.ContainsKey("normal"))
							{
								Normal = RayResults["normal"].As<Vector3>();
								var Projected = DeltaV.Project(Normal);
								dir = DeltaV - 2 * Projected;
								dir = dir.Normalized();
							}

							// Signal the server to spawn back the world ball.
							SignalBounceBack(Position, dir);
						}
						break;
					}
				}
			}
		}
	}
}
