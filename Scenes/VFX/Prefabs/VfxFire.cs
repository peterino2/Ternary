using Godot;

public partial class VfxFire : Node3D
{
	[Export] GpuParticles3D Particle1;
	[Export] GpuParticles3D Particle2;
	[Export] GpuParticles3D Particle3;
	[Export] GpuParticles3D Particle4;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void StopFlames()
	{
		Particle1.Emitting = false;
		Particle2.Emitting = false;
		Particle3.Emitting = false;
		Particle4.Emitting = false;
	}
}
