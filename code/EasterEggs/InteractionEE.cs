using Sandbox;

public sealed class InteractionEE : Component, IInteraction, IEasterEggPiece
{

	public float HoldTime { get; set; }

	public bool Interactable { get; set; } = true;

	public bool Hold { get; set; }

	public GameObject GO { get; set; }

	[Property]
	public bool ShowInteractionUI { get; set; } = true;

	[Property, ShowIf( "ShowInteractionUI", true )]
	public string UIName { get; set; } = "Egg";


	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		
	}


	protected override void OnStart()
	{
		GO = this.GameObject;
	}

	public void OnPiece()
	{
		
		Scene.RunEvent<IEasterEgg>( x => x.OnPieceComplete( this.GameObject, false ) );

	}


	public void OnInteract( Player player )
	{
		OnPiece();
		this.GameObject.Destroy();
	}


	protected override void OnUpdate()
	{

	}
}
