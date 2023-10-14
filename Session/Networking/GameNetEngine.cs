using Godot;
using System;

public partial class GameNetEngine: Node 
{
    public static GameNetEngine StaticInstance;
    public static GameNetEngine Get()
    {
        return StaticInstance;
    }

    public long CommandFrame = 0;
    public double TickDelta = 0.05;
    public double TimeTilTick = 0.0;
    public double GameTime = 0.0;

    Random Rand = new Random();

    // ==============================================
    public delegate void NetTickDelegate(long CommandFrame, double Delta);
    public event NetTickDelegate OnSyncFrame; // called by the server, when it's time to broadcast a sync
    public event NetTickDelegate OnNetTick; // Called by the local clinet it's time to submit a sync

    public override void _Ready() 
    {
        StaticInstance = this;
    }

    public void SetTickDelta(double NewTickDelta)
    {
        TickDelta = NewTickDelta;
    }

    public override void _Process(double delta)
    {
        GameTime  += delta;

        TimeTilTick -= delta; // todo... check what happens when the server fps is too high... or low.
        if(TimeTilTick < 0)
        {
            CommandFrame += 1;
            if(GameSession.Get().PeerId == 1)
            {
                Rpc(nameof(BroadCastCommandFrame), new Variant[] {CommandFrame, GameTime});
                OnSyncFrame?.Invoke(CommandFrame, TickDelta);
            }
            else
            {
                OnNetTick?.Invoke(CommandFrame, TickDelta);
            }

            TimeTilTick = TickDelta;
        }
    }

    public double GetTime() 
    {
        return GameTime;
    }

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void BroadCastCommandFrame(long NewCommandFrame, double SyncGameTime)
	{
        if(Math.Abs(SyncGameTime - GameTime) > 0.05)
        {
            GameTime = SyncGameTime;
        }

        if(GameSession.Get().PeerId == 1)
        {
            return;
        }

        if(NewCommandFrame > CommandFrame + 1)
        {
            if(NewCommandFrame > CommandFrame + 3)
            {
                NU.Warning(
                    "Server is running a command frame much newer than ours, forcefully updating " 
                    + CommandFrame.ToString() + "->" + NewCommandFrame.ToString());
            }

            CommandFrame = NewCommandFrame + 1;
            TimeTilTick = 0;
        }

        if((CommandFrame - NewCommandFrame) > 3)
        {
            NU.Warning(
                "Client is running faster than server? rolling back command frame." 
                + CommandFrame.ToString() + "->" + NewCommandFrame.ToString());
            CommandFrame =  NewCommandFrame;
        }

	}

    // Prediction keys are single ulong,
    // value of 0 means invalid
    public int NewPredictionKey()
    {
        return Rand.Next();
    }
}
