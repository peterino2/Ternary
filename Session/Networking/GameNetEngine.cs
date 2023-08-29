using Godot;
using System;

using System.Net.NetworkInformation;

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

    public delegate void NetTickDelegate(long CommandFrame, double Delta);
    public event NetTickDelegate NetTickables;

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
        TimeTilTick -= delta;
        if(TimeTilTick < 0)
        {
            TimeTilTick = TickDelta;
            CommandFrame += 1;
            if(GameSession.Get().PeerId == 1)
            {
                Rpc(nameof(BroadCastCommandFrame), new Variant[] {CommandFrame});
            }
            NetTickables?.Invoke(CommandFrame, TickDelta);
        }
    }

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void BroadCastCommandFrame(long NewCommandFrame)
	{
        if(NewCommandFrame > CommandFrame)
        {
            NU.Warning(
                "Server is running a command frame newer than ours, forcefully updating " 
                + CommandFrame.ToString() + "->" + NewCommandFrame.ToString());
            CommandFrame =  NewCommandFrame;
            TimeTilTick = 0;
        }

        if((CommandFrame - NewCommandFrame) > 2)
        {
            NU.Warning(
                "Client is running faster than server? rolling back command frame." 
                + CommandFrame.ToString() + "->" + NewCommandFrame.ToString());
            CommandFrame =  NewCommandFrame;
        }
	}

}
