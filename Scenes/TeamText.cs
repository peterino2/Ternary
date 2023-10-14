using Godot;

public partial class TeamText : RichTextLabel
{ 
	int TeamId = -1;

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
				string colorcode = "#aaaaff";
				if(TeamId == 1)
				{
					colorcode = "#ff4444";
				}
				this.Clear();
				this.AppendText("[color=" + colorcode +"]You are on Team: " + (TeamId + 1).ToString() + "[/color]");
			}
		}
	}
}
