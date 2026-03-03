using Sandbox;

public interface IInteraction
{

	public enum InteractionFReason
	{
		None,
		NoMoney,
		NoPower,
		AlreadyOwned,

	}

	public float HoldTime { get; }


	public bool Interactable { get; set; }


	public bool Hold { get; }

	public GameObject GO { get; }


	void OnInteract( Player player );

	void OnInteractionFailed( Player player, InteractionFReason reason );
}
