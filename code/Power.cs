using Sandbox;

public sealed class Power : Component, IInteraction
{
	
	GameModeManager GameModeManager { get; set; }

	public float HoldTime => 0f;

	public void OnInteractionFailed( Player player )
	{
		//could add some feedback here later
	}



	public void OnInteract( Player player )
	{
		
		TurnPowerOn();
	}





	
	private void TurnPowerOn()
	{
		if ( GameModeManager.PowerOn ) return;
		GameModeManager.TurnPowerOn();
	}	


	protected override void OnStart()
	{
		base.OnStart();

	
		GameModeManager = Scene.GetAllComponents<GameModeManager>().First();
		
		
	}


}
