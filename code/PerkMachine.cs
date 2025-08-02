using Sandbox;

public sealed class PerkMachine : Component, IInteraction, IPower
{


	bool hasPower = false;


	public void OnPowerTurnedOn()
	{
		hasPower = true;
	}

	public void OnInteract( Player player )
	{
		if ( !hasPower ) return;
	}

	protected override void OnUpdate()
	{

	}
}
