using Godot;

public partial class PlayerCamera : Node3D
{
	public Node3D ViewTarget;

	[Export] bool Snap = false;
	[Export] bool AutoFindPlayer = true;
	[Export] public Camera3D Camera;

	static PlayerCamera StaticInstance;

	Vector3 Offset = new Vector3(0,0,0);

	float RotationAngle = 0;
	int TeamId = 0;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
		GameState.Get().OnGameStart += OnGameStart;
	}


	public void OnGameStart()
	{
		// team 1, blue is on the left, + on x
		// team 2, red is on the right, - on x

		TeamId = GameSession.Get().GetTeam();
		if(TeamId == 0)
		{
			Offset = new Vector3(5.0f, 0.0f, 0.0f);
		}
		else 
		{
			Offset = new Vector3(-5.0f, 0.0f, 0.0f);
		}
	}

	public static PlayerCamera Get() 
	{
		return StaticInstance;
	}

	public void SetViewTarget(Node3D NewViewTarget)
	{
		ViewTarget = NewViewTarget;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(ViewTarget == null)
		{
			if(LocalPlayerSubsystem.Get().LocalPlayer != null)
			{
				ViewTarget = LocalPlayerSubsystem.Get().LocalPlayer;
			}
			return;
		}

		var ViewPosition =  ViewTarget.Position + Offset;
		if(Snap)
		{
			Position = ViewPosition;
			return;
		}
		var Dir = (ViewPosition - Position).Normalized();
		var Len = (ViewPosition - Position).Length();

		float Speed = (float) Len / 0.5f * 2f;

		Vector3 Velocity = new Vector3(
				Dir.X * (float)delta * Speed,
				Dir.Y * (float)delta * Speed,
				Dir.Z * (float)delta * Speed
		);

		Position = Position + Velocity;
	}
}
