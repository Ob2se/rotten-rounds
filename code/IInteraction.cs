using Sandbox;

public interface IInteraction
{
	public float HoldTime { get; }


	void OnInteract( Player player );

	void OnInteractionFailed( Player player );
}
