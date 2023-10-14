using Godot;

public partial class KillFeedEntry : Control
{
	[Export] RichTextLabel LeftName;
	[Export] RichTextLabel RightName;


	float alpha = 5.0f;

	// Called when the node enters the scene tree for the first time.
	static string t1 = "[color=#aaaaff]";
	static string t2 = "[color=#ff4444]";

	public  void SetFeedEntries(string Name1, int team1, string Name2, int team2)
	{
		LeftName.Clear();
		RightName.Clear();

		NU.Ok("Setting entries");

		var left = t1;
		if(team1 == 1)
		{
			left = t2;
		}

		var right = t1;
		if(team2 == 1)
		{
			right = t2;
		}

		LeftName.AppendText(left + Name1 +"[/color]");
		RightName.AppendText(right + Name2 +"[/color]");
	}

	public override void _Process(double delta)
	{
		if(alpha > 0)
		{
			alpha -= (float) delta;
			Modulate = new Color(1,1,1, Mathf.Min(alpha, 1.0f));
		}
		else 
		{
			QueueFree();
		}
	}

}
