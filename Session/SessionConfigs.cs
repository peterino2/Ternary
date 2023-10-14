using Godot;
using System.IO;
using System.Text.Json;

public partial class SessionConfigs: Node
{

	// ==== statics ====
	public static SessionConfigs StaticInstance;

	public static SessionConfigs Get() { 
		return StaticInstance;
	}
	// === /statics ====
    //
    //
    public class Configs {
        public bool AutoStartServer {get; set;}
        public int AutoStartGamePlayerCount {get; set;}
        public int AutoStartServerPort {get; set;}
        
        public Configs() 
        {
            AutoStartServer = false;
            AutoStartGamePlayerCount = 32;
            AutoStartServerPort = 7777;
        }
    }
    public static Configs LoadSettings(string SettingsPath)
    {
        if(File.Exists(SettingsPath))
        {
            string text = File.ReadAllText(SettingsPath);
            NU.Print("Loading file"+ SettingsPath );
            var configs =  JsonSerializer.Deserialize<Configs>(text);

            NU.Print("AutoStartServer: " + text + " > " + configs.AutoStartServer.ToString());
            return configs;

        }
        else 
        {
            return new Configs();
        }
    }
    

    public Configs SettingsConfig;

    public override void _Ready()
    {
        StaticInstance = this;
        var CurrentDir = System.IO.Directory.GetCurrentDirectory();
        var SettingsPath = CurrentDir + "/Settings.json";
        SettingsConfig = LoadSettings(SettingsPath);

        if(SettingsConfig.AutoStartServer)
        {
            NU.Ok("Settings.json loaded, AutoStartServer=true, starting server in 2 seconds.");
        }
    }

    double AutoStartCountDown = 2.0;

    public override void _Process(double delta)
    {
        if(SettingsConfig.AutoStartServer)
        {
            if(AutoStartCountDown > 0)
            {
                AutoStartCountDown -= delta;
                if(AutoStartCountDown < 0) 
                {
                    GameSession.Get().StartServer(SettingsConfig.AutoStartServerPort);
                }
            }
        }
    }
}
