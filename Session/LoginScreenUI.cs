using Godot;
using System;

public partial class LoginScreenUI : Control
{
	public GameState gameState;

	[Export] private Button ConnectToServer;
	[Export] private Button HostServerButton;
	
	[Export] private TextEdit IPAddressTextEdit;
	[Export] private TextEdit PortTextEdit;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		gameState = GetNode<GameState>("/root/GameState");

		HostServerButton.ButtonDown += OnHostServerButton;
		ConnectToServer.ButtonDown += OnConnectToServerButton;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnHostServerButton()
	{
		gameState.StartServer(Int32.Parse(PortTextEdit.Text));
	}

	private void OnConnectToServerButton()
	{
		gameState.ConnectAsClient(IPAddressTextEdit.Text, Int32.Parse(PortTextEdit.Text));
	}
}
