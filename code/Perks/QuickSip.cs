using Sandbox;

public sealed class QuickSip : Perk
{

	
	protected override void OnStart()
	{
		base.OnStart();
		// Perk state (HasQuickSip, PerkIcons) is now applied via Player.ApplyPerkState broadcast
	}



	protected override void OnUpdate()
	{

	}
}
