
using Godot;

public partial class TeamSpawnArea : Node3D
{
    [Export] int TeamId = 0; //  0 for team 1, 1 for team 2

	public override void _Ready()
	{
        GameState.Get().RegisterSpawnArea(TeamId, GlobalPosition);
    }
}
