using Sandbox;

public sealed class QuickRevivePerk : Perk
{


	protected override void OnStart()
	{
		base.OnStart();
		// Perk state (HasIcyAid, PerkIcons) is now applied via Player.ApplyPerkState broadcast
	}

	protected override void OnUpdate()
	{

	}
}
