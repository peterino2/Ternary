using Godot;

public partial class IngameUI : Control
{
	public static IngameUI StaticInstance;
	
	public static IngameUI Get() 
	{
		return StaticInstance;
	}

	[Export] ProgressBar ChargeBar;
	[Export] ProgressBar DodgeCooldown;
	[Export] ProgressBar BlockCooldown;
	[Export] ProgressBar BlockDuration;

	[Export] RichTextLabel Time;
	[Export] RichTextLabel PlayerCount;
	[Export] TextureRect YouWin;
	[Export] TextureRect YouLose;

	[Export] FlowContainer KillFeed;
	[Export] PackedScene KillFeedEntryScene;
	
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
		GameState.Get().OnKill += OnPlayerKilled;
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

	public void OnPlayerKilled(Player Killer, Player Killed)
	{
		NU.Ok("[ui] player killed");
		var children = KillFeed.GetChildren();

		if(children.Count > 5)
		{
			// children[0].QueueFree();
		}

		var newKillFeedEntry = KillFeedEntryScene.Instantiate() as KillFeedEntry;
		var leftName = GameSession.Get().NamesById[Killer.OwnerId];
		var leftTeam = Killer.TeamId;
		var rightName = GameSession.Get().NamesById[Killed.OwnerId];
		var rightTeam = Killed.TeamId;
		newKillFeedEntry.SetFeedEntries(leftName, leftTeam, rightName, rightTeam);

		KillFeed.AddChild(newKillFeedEntry);
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

		DodgeCooldown.Value = PlayerRef.CurrentDodgingCooldown / PlayerRef.DodgingCooldown;

		BlockCooldown.Value = PlayerRef.CurrentBlockingCooldown  / PlayerRef.BlockingCooldown;

		BlockDuration.Visible = PlayerRef.CurrentBlockingDuration > 0;
		BlockDuration.Value = PlayerRef.CurrentBlockingDuration  / PlayerRef.BlockingDuration;

		Time.Clear();
		Time.AppendText("[center]" + ((int) GameState.Get().GameTime)+"[/center]");
	}
}
