using Sandbox;

public sealed class Power : Component, IInteraction
{
	
	GameModeManager GameModeManager { get; set; }
	public bool Interactable { get; set; } = true;


	[Property]
	private Collider InteractionTrigger { get; set; }


	[Property]
	private SkinnedModelRenderer PowerBoxMesh { get; set; }


	[Property]
	public bool Hold { get; set; }
	public GameObject GO => this.GameObject;

	public float HoldTime => 0f;






	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		//could add some feedback here later
	}



	public void OnInteract( Player player )
	{
		
		TurnPowerOn();
	}



	private void InteractionTriggerBoxEnter( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if(player != null)
		{

			if(Interactable)
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
			player.CurrentInteraction = null;
		}

	}



	[Rpc.Host]
	private void TurnPowerOn()
	{
		if ( GameModeManager.PowerOn ) return;
		PlayAnimation();
		GameModeManager.TurnPowerOn();

		Setvars();
	}

	[Rpc.Broadcast]
	private void Setvars()
	{
		Interactable = false;
	}



	[Rpc.Broadcast]
	private void PlayAnimation()
	{
		PowerBoxMesh.Sequence.Name = "PowerOn";
	}


	protected override void OnStart()
	{
		base.OnStart();

		InteractionTrigger.OnObjectTriggerEnter += InteractionTriggerBoxEnter;
		InteractionTrigger.OnObjectTriggerExit += InteractionTriggerBoxExit;


		GameModeManager = Scene.GetAllComponents<GameModeManager>().First();

		if(GameModeManager.PowerOn)
		{
			Setvars();
		}


	}


}
