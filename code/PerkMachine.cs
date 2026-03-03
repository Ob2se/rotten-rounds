using System;
using Sandbox;

public sealed class PerkMachine : Component, IInteraction, IPower
{

	[Property]
	public PerkID PerkType { get; set; } = PerkID.None;


	[Property]
	public bool Hold { get; set; }

	[Property]
	private Collider InteractionTriggerbox { get; set; }

	public bool Interactable { get; set; } = true;

	[Property, Sync]
	public int Cost { get; set; } = 500;


	public float HoldTime => 0f;
	public GameObject GO => this.GameObject;

	[Sync]
	public bool hasPower { get; set; } = false;


	public GameModeManager GameMode;


	[Rpc.Host]
	public void OnPowerTurnedOn()
	{
		hasPower = true;
	}


	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		//could add some feedback here later

		switch ( reason ) 
		{
			case IInteraction.InteractionFReason.NoMoney:
				Log.Info( "Interaction Failed: No Money" );
				return;

			case IInteraction.InteractionFReason.AlreadyOwned:
				Log.Info( "Interaction Failed: Already Owned" );
				return;

			case IInteraction.InteractionFReason.NoPower:
				break;

			default:
				return;
		}



		Log.Info( "Interaction Failed: No Power" );
	}

	public void OnInteract( Player player )
	{

		if(player.Points < Cost)
		{
			OnInteractionFailed( player, IInteraction.InteractionFReason.NoMoney );
			return;
		}

		if ( !hasPower )
		{
			return;
		}

		switch ( PerkType )
		{
			case PerkID.QuickRevive:
				var perk = player.GetComponentInChildren<QuickRevivePerk>();
				if ( perk != null )
				{
					OnInteractionFailed( player, IInteraction.InteractionFReason.AlreadyOwned );
					return;
				}
				if ( perk == null )
				{
					perk = player.AddComponent<QuickRevivePerk>( false );
					perk.targetPlayer = player;
					perk.Enabled = true;

					player.RemovePoints( Cost );
					//player.RemovePoints( PerkDatabase.GetData( PerkID.QuickRevive ).PerkCost );
					player.PlayChaChing();
					player.CurrentInteraction = null;

				}

				break;
			default:
				break;
		}
	}






	[Rpc.Host]
	private void GivePerkToPlayer( Player player )
	{
		
	}



	private void InteractionTriggerBoxEnter( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if ( player != null )
		{
			Interactable = GameMode.PowerOn;
			if ( Interactable )
			{
				player.CurrentInteraction = this;
			}
			

		}
		
		
	}
	private void InteractionTriggerBoxExit( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if ( player != null )
		{
			Interactable = false;
			player.CurrentInteraction = null;
		}
	}



	protected override void OnStart()
	{
		InteractionTriggerbox.OnObjectTriggerEnter += InteractionTriggerBoxEnter;
		InteractionTriggerbox.OnObjectTriggerExit += InteractionTriggerBoxExit;

		GameMode = Scene.GetAllComponents<GameModeManager>().FirstOrDefault();



	}

	protected override void OnUpdate()
	{

	}
}
