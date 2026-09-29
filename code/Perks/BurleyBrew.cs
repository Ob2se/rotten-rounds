using Sandbox;

public sealed class BurleyBrew : Perk
{


	protected override void OnStart()
	{
		base.OnStart();
		// Perk state (HasBurleyBrew, PerkIcons) is now applied via Player.ApplyPerkState broadcast
		if ( targetPlayer != null )
		{
			targetPlayer.SetHealth( 150 );
		}
	}

	protected override void OnUpdate()
	{

	}
}
