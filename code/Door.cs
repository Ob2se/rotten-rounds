using Sandbox;
using System.Numerics;

public sealed class Door : Component, IInteraction, IPower
{

	[Sync, Property]
	public int DoorPrice { get; set; }

	[Sync, Change( "OnDoorOpened" )]
	public bool Opened { get; set; } = false;

	[Property]
	private SkinnedModelRenderer DoorModel { get; set; }

	public bool Interactable { get; set; } = true;

	[Property]
	public bool NeedsPower { get; set; }

	[Property]
	private Collider InteractionTrigger { get; set; }


	[Sync]
	public bool Powered { get; set; } = false;


	[Property]
	private List<GameObject> AssociatedSpawns;

	[Property]
	public BoxCollider BoxCollida { get; set; }

	public float HoldTime => 0f;

	[Property]
	public bool Hold { get; set; }

	public GameModeManager GameMode;

	public GameObject GO => this.GameObject;

	[Property]
	public bool UseDoorAnimation { get; set; } = false;


	[Property]
	public bool ConnectedToOtherDoors { get; set; } = false;

	[Property, HideIf( "ConnectedToOtherDoors", false ), Validate( "CheckConnectedDoorsNoPower", "ERROR: One of the connected doors requires power while this one does not!", LogLevel.Error ), Validate( "CheckConnectedDoorsPowered", "ERROR: One of the connected doors does not require power while this one does!", LogLevel.Error )]
	public List<Door> ConnectedDoors { get; set; }


	private bool CheckConnectedDoorsPowered( List<Door> connectedDoors )
	{
		if ( NeedsPower )
		{
			if ( ConnectedToOtherDoors )
			{
				foreach ( var door in connectedDoors )
				{
					if ( !door.NeedsPower ) return false;
				}
			}
		}
		return true;
	}

	private bool CheckConnectedDoorsNoPower( List<Door> connectedDoors )
	{
		if ( !NeedsPower )
		{
			if ( ConnectedToOtherDoors )
			{
				foreach ( var door in connectedDoors )
				{
					if ( door.NeedsPower ) return false;
				}
			}
		}
		return true;
	}


	protected override void DrawGizmos()
	{
		if ( !Scene.Editor.Selection.Contains( this.GameObject ) ) return;

		if ( AssociatedSpawns.Count() == 0 ) return;
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.IgnoreDepth = true;
		foreach ( var x in AssociatedSpawns )
		{
			Gizmo.Draw.Line(
			GameObject.Transform.World.Position,
			x.Transform.World.Position
		);
		}
	}



	private void OnDoorOpened()
	{
		//change to animation eventually
		if ( UseDoorAnimation )
		{
			DoorModel.Set( "b_open", true );

			return;
		}
		DeleteDoor();

	}

	[Rpc.Host]
	private void DeleteDoorCollider()
	{
		BoxCollida.Destroy();
	}

	[Rpc.Broadcast]
	private void DeleteDoor()
	{
		GameObject.Destroy();
	}

	
	private void OpenDoor( Player player )
	{

		player.RemovePoints( DoorPrice );
		player.PlayChaChing();
		player.CurrentInteraction = null;
		DeleteDoorCollider();
		if ( ConnectedToOtherDoors )
		{
			foreach ( var door in ConnectedDoors )
			{
				door.OpenDoorConnection();
			}
		}
		ActivateSpawns();
		
	}


	public void OpenDoorConnection()
	{
		ActivateSpawns();
	}


	[Rpc.Host]
	private void ActivateSpawns()
	{
		if ( AssociatedSpawns.Count() <= 0 )
		{
			Opened = true;
			return;
		}
		foreach ( var x in AssociatedSpawns )
		{
			x.GetComponent<ZombieSpawn>().activated = true;
		}
		Opened = true;
	}



	public void OnPowerTurnedOn()
	{
		TurnPowerOn();
	}


	[Rpc.Host]
	private void TurnPowerOn()
	{
		Powered = true;
	}

	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		//could add some feedback here later
	}

	public void OnInteract(Player player)
	{

		if(player.Points < DoorPrice)
		{
			OnInteractionFailed( player, IInteraction.InteractionFReason.NoMoney );
			return;
		}

		if ( !Powered && NeedsPower)
		{
			return;
		}

		OpenDoor(player);

	}

	private void InteractionTriggerEnter( GameObject obj )
	{
		var player = obj.GetComponentInParent<Player>();
		if( player != null )
		{
			if ( Interactable )
			{
				player.CurrentInteraction = this;
			}
		}
	}


	private void InteractionTriggerExit( GameObject obj )
	{
		var player = obj.GetComponentInParent<Player>();
		if ( player != null )
		{
			player.CurrentInteraction = null;
		}
	}


	


	
	protected override void OnStart()
	{
		InteractionTrigger.OnObjectTriggerEnter += InteractionTriggerEnter;
		InteractionTrigger.OnObjectTriggerExit += InteractionTriggerExit;

		GameMode = Scene.GetAllComponents<GameModeManager>().FirstOrDefault();
		if ( !NeedsPower )
		{
			Interactable = true;
		}
	}

	protected override void OnUpdate()
	{

	}
}
