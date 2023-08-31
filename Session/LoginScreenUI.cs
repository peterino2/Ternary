using Godot;
using System;

public partial class LoginScreenUI : Control
{
	// === statics ===
	static LoginScreenUI StaticInstance;
	
	public static LoginScreenUI Get()
	{
		return StaticInstance;
	}
	// === statics ===
	//
	[Export] private Button ConnectToServer;
	[Export] private Button HostServerButton;
	
	[Export] private TextEdit IPAddressTextEdit;
	[Export] private TextEdit PortTextEdit;
	[Export] private TextEdit PlayerName;

	[Export] private Button ErrorLabel;
	[Export] private Button RequestGameStartButton;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		StaticInstance = this;
		HostServerButton.ButtonDown += OnHostServerButton;
		ConnectToServer.ButtonDown += OnConnectToServerButton;
        RequestGameStartButton.ButtonDown += OnRequestGameStart;

		ErrorLabel.ButtonDown += OnErrorAck;
	}

    public void OnRequestGameStart() 
    {
        GameSession.Get().RequestGameStartFromClient();
    }

	private void OnHostServerButton()
	{
		GameSession.Get().StartServer(Int32.Parse(PortTextEdit.Text));
	}

	private void OnConnectToServerButton()
	{
		GameSession.Get().PlayerName = PlayerName.Text;
		GameSession.Get().ConnectAsClient(IPAddressTextEdit.Text, Int32.Parse(PortTextEdit.Text));
	}

	public void SetVisiblityAndProcess(bool NewVisibility)
	{
		Visible = NewVisibility;
		SetProcess(NewVisibility);
	}

	public void ShowError(string ErrorMessage)
	{
		ErrorLabel.Text = ErrorMessage;
		ErrorLabel.Visible = true;
		ErrorLabel.SetProcess(true);
	}

	public void OnErrorAck()
	{
		ErrorLabel.Visible = false;
		ErrorLabel.SetProcess(false);
	}

    public void UpdateSessionState()
    {
        var Session = GameSession.Get();

        if(Session.LoginVerifiedClient)
        {
            IPAddressTextEdit.Editable = false;
            PortTextEdit.Editable = false;
            PlayerName.Editable = false;
            RequestGameStartButton.Visible = true;
        }
    }
}
