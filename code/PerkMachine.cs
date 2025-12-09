using System;
using Sandbox;

public sealed class PerkMachine : Component, IInteraction, IPower
{

	[Property]
	public PerkID PerkType { get; set; } = PerkID.None;


	public float HoldTime => 0f;


	bool hasPower = false;


	public void OnPowerTurnedOn()
	{
		hasPower = true;
	}


	public void OnInteractionFailed( Player player )
	{
		//could add some feedback here later
		Log.Info( "Interaction Failed: No Power" );
	}

	public void OnInteract( Player player )
	{
		if ( !hasPower )
		{
			OnInteractionFailed( player );
			return;
		}

		switch ( PerkType )
		{
			case PerkID.QuickRevive:
				var perk = player.GetComponentInChildren<QuickRevivePerk>();
				if ( perk == null )
				{
					perk = player.AddComponent<QuickRevivePerk>(false);
					perk.targetPlayer = player;
					perk.Enabled = true;

					player.RemovePoints( PerkDatabase.GetData( PerkID.QuickRevive ).PerkCost );
					player.PerkListIconsChanged();

				}
				
				break;
			default:
				break;
		}
		

	}

	protected override void OnUpdate()
	{

	}
}
