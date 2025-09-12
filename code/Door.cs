using Sandbox;
using System.Numerics;

public sealed class Door : Component, IInteraction, IPower
{

	[Sync, Property]
	public int DoorPrice { get; set; }

	[Sync, Change( "OnDoorOpened" )]
	public bool Opened { get; set; } = false;

	[Property]
	private ModelRenderer DoorModel {  get; set; }

	[Property]
	private bool NeedsPower { get; set; }

	[Sync]
	public bool Powered { get; set; } = false;

	[Property]
	private List<GameObject> AssociatedWindows;

	[Property]
	private List<GameObject> AssociatedSpawns;

	private void OnDoorOpened()
	{
		//change to animation eventually
		DeleteDoor();
		
	}

	[Rpc.Broadcast]
	private void DeleteDoor()
	{
		GameObject.Destroy();
	}

	[Rpc.Host]
	private void OpenDoor( Player player )
	{
		Log.Info( "door interacted with" );
		if ( player.Points - DoorPrice < 0 )
		{
			Log.Info( "broke" );
			return;
		}
		player.RemovePoints( DoorPrice );
		Opened = true;
		foreach ( var x in AssociatedSpawns )
		{
			x.GetComponent<ZombieSpawn>().activated = true;
		}
	}


	public void OnPowerTurnedOn()
	{
		TurnPowerOn();
	}


	[Rpc.Host]
	private void TurnPowerOn()
	{
		if ( NeedsPower && !Powered )
		{
			Powered = true;
		}
	}


	public void OnInteract(Player player)
	{

		if ( NeedsPower && !Powered )
		{
			//need some indication that it needs power 
			return;
		} 
		OpenDoor(player);

	}

	protected override void OnUpdate()
	{

	}
}
