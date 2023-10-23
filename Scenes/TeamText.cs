using Godot;

public partial class TeamText : RichTextLabel
{
	int TeamId = -1;
	public const string SpectatorColor = "#000000";
	public const string Team1Color = "#aaaaff";
	public const string Team2Color = "#ff4444";
	
	public static readonly string[] ColorsList = {
		"#cccccc",
		"#aaaaff",
		"#ff4444"
	};

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if(GameSession.Get().PeerId != 0 && GameSession.Get().PeerId != 1)
		{
			var newTeamId = GameSession.Get().GetTeam();
			if(newTeamId != TeamId)
			{
				TeamId = newTeamId;
				string colorcode = ColorsList[TeamId];
				this.Clear();
				if(TeamId != 0)
				{
					this.AppendText("[color=" + colorcode +"]You are on Team: " + (TeamId).ToString() + "[/color]");
				}
				else 
				{
					this.AppendText("[color=" + colorcode + "]You are a Spectator[/color]");
				}
			}
		}
	}
}
