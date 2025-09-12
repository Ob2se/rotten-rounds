using Sandbox;

public sealed class Power : Component, IInteraction
{
	[Sync]
	GameModeManager GameModeManager { get; set; }

	public void OnInteract( Player player )
	{
		
		TurnPowerOn();
	}





	[Rpc.Host]
	private void TurnPowerOn()
	{
		if ( GameModeManager.PowerOn ) return;
		GameModeManager.TurnPowerOn();
	}	


	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost )
		{
			GameModeManager = Scene.GetAllComponents<GameModeManager>().First();
		}
		
	}


}
