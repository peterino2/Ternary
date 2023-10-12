using Godot;
using System;

public partial class WorldBall : RigidBody3D
{
	Transform3D TransformSync;
	Vector3 VelocitySync;
	Vector3 AngularVelocitySync;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GameNetEngine.Get().OnSyncFrame += OnSyncFrame; 
	}

	public override void _ExitTree() 
	{
		GameNetEngine.Get().OnSyncFrame -= OnSyncFrame; 
	}

	public void OnSyncFrame(long CommandFrame, double Delta)
	{
		// RPC to clients here
		Rpc(nameof(RecieveGameStateClient), new Variant[] {
			Transform, 
			LinearVelocity,
			AngularVelocity
		});
	}


	public override void _IntegrateForces(PhysicsDirectBodyState3D state )
	{
		if(GameSession.Get().IsServer())
		{
			return;
		}

		state.LinearVelocity = VelocitySync;
		state.Transform = TransformSync;
		state.AngularVelocity = AngularVelocitySync;
	}
    

	public void GetPickedUp(Player PickerUpper)
    {
        RpcId(1, nameof(GetPickedUpServer), new Variant[]{
                
        });
    }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void GetPickedUpServer()
    {
        var Sender = Multiplayer.GetRemoteSenderId();
        if(Sender == 1)
        {
            return;
        }

        Visible = false;
        CollisionLayer = 0;
        CollisionMask = 0;
        Position = new Vector3(0,-30.0f,0);
        Freeze = true;
    }

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void RecieveGameStateClient(
		Transform3D NewTransformSync,
		Vector3 NewVelocitySync,
		Vector3 NewAngularVelocitySync
	){
		TransformSync = NewTransformSync;
		VelocitySync = NewVelocitySync;
		AngularVelocitySync = NewAngularVelocitySync;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
