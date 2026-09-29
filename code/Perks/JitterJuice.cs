using Sandbox;

public sealed class JitterJuice : Perk
{


	protected override void OnStart()
	{
		base.OnStart();
		// Perk state (HasJitterJuice, PerkIcons) is now applied via Player.ApplyPerkState broadcast
	}


	protected override void OnUpdate()
	{

	}
}
