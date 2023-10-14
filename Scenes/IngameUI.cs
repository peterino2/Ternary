using Godot;

public partial class IngameUI : Control
{
	public static IngameUI StaticInstance;
	
	public static IngameUI Get() 
	{
		return StaticInstance;
	}

	[Export] ProgressBar ChargeBar;
	[Export] RichTextLabel Time;
	[Export] RichTextLabel PlayerCount;
	[Export] TextureRect YouWin;
	[Export] TextureRect YouLose;
	
	Player PlayerRef;

	public void SetupPlayer(Player playerRef)
	{
		PlayerRef = playerRef;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
		GameState.Get().OnGameStart += OnGameStart;
		GameState.Get().OnPlayerCountChanged += OnPlayerCountChanged;
		GameState.Get().OnGameWin += OnGameWin;
		ChargeBar.Visible = false;
		OnPlayerCountChanged();
	}

	public void OnGameWin(int Team)
	{
		if(Team == PlayerRef.TeamId)
		{
			YouWin.Visible = true;
		}

		if(Team != PlayerRef.TeamId)
		{
			YouLose.Visible = true;
		}
	}

	public void OnPlayerCountChanged()
	{
		var t1 = GameState.Get().SyncedPlayerCountTeam1;
		var t2 = GameState.Get().SyncedPlayerCountTeam2;

		PlayerCount.Clear();
		PlayerCount.AppendText("[center][color=#aaaaff]"+t1.ToString()+"[/color]vs[color=#ff4444]"+t2.ToString()+"[/color][/center]");
	}

	public void OnGameStart()
	{
		Visible = true;

		YouWin.Visible = false;
		YouLose.Visible = false;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(PlayerRef == null)
			return;
		if(PlayerRef.ChargeTime > 0)
		{
			ChargeBar.Visible = true;
			ChargeBar.Value = (PlayerRef.ChargeTime / PlayerRef.ChargeTimeToThrow ) * 100;
		}
		else 
		{
			ChargeBar.Visible = false;
		}
		
		Time.Clear();
		Time.AppendText("[center]" + ((int) GameState.Get().GameTime)+"[/center]");
	}
}
