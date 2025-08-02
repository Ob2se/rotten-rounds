using Sandbox;

public sealed class Power : Component, IInteraction
{
	[Property]
	GameModeManager GameModeManager { get; set; }

	public void OnInteract( Player player )
	{
		if ( GameModeManager.PowerOn ) return;
		GameModeManager.TurnPowerOn();
	}

}
