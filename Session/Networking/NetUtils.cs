using Godot;
using System;

public class NU
{		
	public static void Print(string printString)
	{
		if(GameSession.Get().PeerId == 1)
		{
			GD.PrintRich($"[color=white]Server: " + printString);
		}
		else
		{
			GD.PrintRich($"[color=white]Client [" + GameSession.Get().PeerId.ToString() + "]: " + printString);
		}
	}

	public static void Ok(string printString)
	{
		if(GameSession.Get().PeerId == 1)
		{
			GD.PrintRich($"[color=green]Server: " + printString);
		}
		else 
		{
			GD.PrintRich($"[color=green]Client [" + GameSession.Get().PeerId.ToString() + "]: " + printString);
		}
	}
    
	public static void Warning(string printString)
	{
		if(GameSession.Get().PeerId == 1)
		{
			GD.PrintRich($"[color=yellow]Server: " + printString);
		}
		else 
		{
			GD.PrintRich($"[color=yellow]Client [" + GameSession.Get().PeerId.ToString() + "]: " + printString);
		}
	}

	public static void Error(string printString)
	{
		if(GameSession.Get().PeerId == 1)
		{
			GD.PrintRich($"[color=red]Server: " + printString);
		}
		else 
		{
			GD.PrintRich($"[color=red]Client [" + GameSession.Get().PeerId.ToString() + "]: " + printString);
		}
	}
}
