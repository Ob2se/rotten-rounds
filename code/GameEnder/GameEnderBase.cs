using Sandbox;
using System;
using System.Linq;

public class GameEnderBase : Component, IInteraction
{

	[Property]
	public bool Hold { get; set; } = false;

	[Property, ShowIf("Hold", true)]
	public float HoldTime { get; set; } = 1f;

	[Property]
	public bool Interactable { get; set; } = true;

	public GameObject GO => this.GameObject;

	

	[Rpc.Host]
	virtual public void StartGameEnder()
	{
		
	}


	public void OnInteract( Player player )
	{
		StartGameEnder();
	}


	virtual public void GameEndSuccess()
	{
		
	}

	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		
	}


	protected override void OnUpdate()
	{

	}
}
