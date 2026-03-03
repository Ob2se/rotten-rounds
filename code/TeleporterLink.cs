using Sandbox;

public sealed class TeleporterLink : Component, ITeleporter, IInteraction, IPower
{



	public float HoldTime { get; set; } = 0f;

	public bool Interactable { get; set; } = true;

	public bool Hold { get; set; } = false;

	public GameObject GO => this.GameObject;

	[Sync]
	public bool LinkStarted { get; set; } = false;

	[Sync]
	public bool Powered { get; set; } = false;


	TimeSince TimeSinceLinkStarted { get; set; }

	[Property]
	public List<Teleporter> LinkedTeleporters { get; set; }


	[Sync]
	public Teleporter CurrentLink { get; set; }

	public void ActivateTeleporter( Teleporter teleporter )
	{
		
	}

	public void LinkTeleporter( Teleporter teleporter )
	{
		var teleporterindex = LinkedTeleporters.FindIndex( t => t == teleporter );


		StartLink(teleporterindex);

	}


	[Rpc.Host]
	public void StartLink(int Teleporter)
	{
		TimeSinceLinkStarted = 0;
		LinkStarted = true;
		CurrentLink = LinkedTeleporters[Teleporter];
		Log.Info("Link started with " + CurrentLink.ToString());

	}


	public void OnInteract( Player player )
	{
		if ( !Powered )
		{
			OnInteractionFailed( player, IInteraction.InteractionFReason.NoPower );
		}
		if ( !LinkStarted )
		{
			return;
		}
		if ( LinkStarted )
		{
			ActivateLink();
		}
	}

	[Rpc.Host]
	public void ActivateLink()
	{
		if( CurrentLink == null ) return;
		Log.Info( "activated link" );
		LinkStarted = false;
		CurrentLink.TeleporterActivate();
	}


	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{

	}


	public void OnPowerTurnedOn()
	{
		TurnOnPower();
	}

	[Rpc.Host]
	public void TurnOnPower()
	{
		Powered = true;
	}

	protected override void OnUpdate()
	{

	}
}
