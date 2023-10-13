using Godot;
using System;

public partial class WorldBall : RigidBody3D
{
	Transform3D TransformSync;
	Vector3 VelocitySync;
	Vector3 AngularVelocitySync;

    bool NewStateRecieved = false;

    bool LastFreeze = false;

	Transform3D LastTransformSync;
	Vector3 LastVelocitySync;
	Vector3 LastAngularVelocitySync;

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
        // NU.Ok("maybe broadcasting: " + TransformSync.ToString() + " : " + LastTransformSync.ToString());

        TransformSync = Transform;
        VelocitySync = LinearVelocity;
        AngularVelocitySync = AngularVelocity;

        if(!LastTransformSync.IsEqualApprox(TransformSync) || Freeze || !Visible)
        {
            Rpc(nameof(RecieveGameStateClient), new Variant[] {
                TransformSync, 
                VelocitySync,
                AngularVelocitySync
            });

            LastFreeze = Freeze;
            LastTransformSync = TransformSync;
            LastVelocitySync = VelocitySync;
            LastAngularVelocitySync = AngularVelocitySync;
        }
	}

	public override void _IntegrateForces(PhysicsDirectBodyState3D state )
	{
		if (GameSession.Get().IsServer())
		{
			return;
		}

        if(NewStateRecieved)
        {
            state.LinearVelocity = VelocitySync;
            state.Transform = TransformSync;
            state.AngularVelocity = AngularVelocitySync;
            NewStateRecieved = false;
        }
	}

	public void GetPickedUp(Player PickerUpper)
    {
        RpcId(1, nameof(GetPickedUpServer), new Variant[]{
            PickerUpper.GetPath()
        });
    }

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void GetPickedUpServerMulticast(string PlayerPath)
    {
        if(GameSession.Get().IsServer())
        {
            return;
        }

        var PlayerRef = GetNode<Player>(PlayerPath);

        if (PlayerRef != null)
        {
            PlayerRef.PickedUpBall = this;
            PlayerRef.HoldingBall = true;
        }

        Visible = false;
        CollisionLayer = 0;
        CollisionMask = 0;
        Position = new Vector3(0, -30.0f, 0);
        Freeze = true;
    }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void GetPickedUpServer(string PlayerPath)
    {
        if(!GameSession.Get().IsServer())
        {
            return;
        }

        var PlayerRef = GetNode<Player>(PlayerPath);

        if (PlayerRef != null)
        {
            NU.Ok("Recieved Pickup mesage from: " + PlayerRef.DebugName);
            PlayerRef.PickedUpBall = this;
        }

        Visible = false;
        CollisionLayer = 0;
        CollisionMask = 0;
        Position = new Vector3(0, -30.0f, 0);
        Freeze = true;

        Rpc(nameof(GetPickedUpServerMulticast), new Variant[]{
            PlayerPath
        });
    }

    public void ReEnableAt(Vector3 ImpactPoint)
    {
        Position = ImpactPoint + new Vector3(0, 1.0f, 0);
        Visible = true;
        CollisionLayer = 2;
        CollisionMask = 2;
        Freeze = false;

        Rpc(nameof(ReEnableAtMulticast), new Variant[]{
            ImpactPoint,
        });
    }


	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReEnableAtMulticast(Vector3 ImpactPoint)
    {
        Position = ImpactPoint + new Vector3(0, 1.0f, 0);
        Visible = true;
        CollisionLayer = 2;
        CollisionMask = 2;
        Freeze = false;
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
        NewStateRecieved = true;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
