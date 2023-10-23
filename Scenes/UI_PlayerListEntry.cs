using Godot;

public partial class UI_PlayerListEntry: Control 
{
	[Export] private RichTextLabel TeamLabel;
	[Export] private RichTextLabel PlayerNameLabel;
	[Export] private RichTextLabel PlayerIdLabel;
	[Export] private RichTextLabel PingLabel;
	[Export] private Button KickButton;

	public long Id = 0;
	public long TeamId = -1;

	public string PlayerName = "<Unknown>";


	public override void _Ready()
	{
		KickButton.ButtonDown += KickPlayer;
	}

	public override void _Process(double delta)
	{
		if(!GameSession.Get().TeamIds.ContainsKey(Id))
		{
			return;
		}

		var NewTeamId = GameSession.Get().TeamIds[Id];
		if(TeamId != NewTeamId)
		{
			TeamId = NewTeamId;
            string colorcode = TeamText.ColorsList[NewTeamId];
			TeamLabel.Clear();
			TeamLabel.AppendText("[color=" + colorcode +"]Team: " + (TeamId).ToString() + "[/color]");
		}
	}

	public void KickPlayer()
	{
	}
	
	public void SetName(string NewName)
	{
		PlayerName = NewName;
		PlayerNameLabel.Text = NewName;
	}

	public void SetId(long NewId) 
	{
		Id = NewId;
		PlayerIdLabel.Text = NewId.ToString();

	}

	public void UpdatePing()
	{
		var Peer = (ENetMultiplayerPeer)Multiplayer.MultiplayerPeer;
		double LastRoundTripTime = Peer.GetPeer((int)Id).GetStatistic(ENetPacketPeer.PeerStatistic.LastRoundTripTime);
		PingLabel.Text = LastRoundTripTime + "ms";
	}
}
