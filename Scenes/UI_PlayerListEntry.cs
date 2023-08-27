using Godot;
using System;
using System.Collections.Generic;

public partial class UI_PlayerListEntry: Control 
{
	[Export] private RichTextLabel PlayerNameLabel;
	[Export] private RichTextLabel PlayerIdLabel;
	[Export] private RichTextLabel PingLabel;
	[Export] private Button KickButton;

	public long Id = 0;
	public string PlayerName = "<Unknown>";


	public override void _Ready()
	{
		KickButton.ButtonDown += KickPlayer;
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

	double PingTime = 1.0;

	public override void _Process(double Delta)
	{
		PingTime -= Delta;
		if(PingTime < 0 )
		{
			PingTime = 1.0;
			UpdatePing();
		}
	}

	public void UpdatePing()
	{
		var Peer = (ENetMultiplayerPeer)Multiplayer.MultiplayerPeer;
		double LastRoundTripTime = Peer.GetPeer((int)Id).GetStatistic(ENetPacketPeer.PeerStatistic.LastRoundTripTime);
		PingLabel.Text = LastRoundTripTime + "ms";
	}
}
