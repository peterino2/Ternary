using Godot;

public partial class LocalLobbyControls : Control
{
	static LocalLobbyControls StaticInstance;

	public static LocalLobbyControls Get()
	{
		return StaticInstance;
	}

	[Export] RichTextLabel Team1Label;
	[Export] RichTextLabel Team2Label;
	[Export] RichTextLabel SpectatorsLabel;

	[Export] VFlowContainer Team1PlayerList;
	[Export] VFlowContainer Team2PlayerList;

	[Export] Button JoinTeam1Button;
	[Export] Button JoinTeam2Button;
	[Export] Button JoinSpectatorsButton;

	[Export] Button DisconnectButton;

	[Export] PackedScene PlayerTeamEntry;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
		Visible = false;
		ClearChildren();
		GameSession.Get().OnNewServerState += OnNewServerState_c;
		GameState.Get().OnGameStart += OnGameStart;
		JoinTeam1Button.ButtonDown += OnJoinTeam1;
		JoinTeam2Button.ButtonDown += OnJoinTeam2;
		JoinSpectatorsButton.ButtonDown += OnJoinSpectator;
		DisconnectButton.ButtonDown += OnDisconnect;
	}

	void OnGameStart()
	{
		SetVisiblityAndProcess(false);
	}

	void ClearChildren()
	{
		foreach(var child in Team1PlayerList.GetChildren())
		{
			child.QueueFree();
		}
		foreach(var child in Team2PlayerList.GetChildren())
		{
			child.QueueFree();
		}
	}

	public void OnJoinTeam1()
	{
		GameSession.Get().RequestTeamChange(1);
	}

	public void OnJoinTeam2()
	{
		GameSession.Get().RequestTeamChange(2);
	}

	public void OnJoinSpectator()
	{
		GameSession.Get().RequestTeamChange(0);
	}

	public void OnNewServerState_c()
	{
		ClearChildren();
		foreach(var kv in GameSession.Get().TeamIds)
		{
			var name = GameSession.Get().NamesById[kv.Key];
			var id = kv.Key;
			var team = kv.Value;
			NU.Ok("Lobby> OnNewServerState> " + name + ": " + kv.Key + " team: " + kv.Value);

			var colorcode = TeamText.ColorsList[team];
			var newLLE = PlayerTeamEntry.Instantiate<LocalLobbyEntry>();

			newLLE.PlayerName.Clear();
			newLLE.PlayerName.AppendText("[color="+ colorcode +"]" + name + "[/color]");

			if(team == 1)
			{
				Team1PlayerList.AddChild(newLLE);
			}

			if(team == 2)
			{
				Team2PlayerList.AddChild(newLLE);
			}
		}
	}
	
	public void OnDisconnect() 
	{
		GameSession.Get().CloseClient();
		LoginScreenUI.Get().SetVisiblityAndProcess(true);
		SetVisiblityAndProcess(false);
	}

	public void SetVisiblityAndProcess(bool NewVisibility)
	{
		Visible = NewVisibility;
		SetProcess(NewVisibility);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
