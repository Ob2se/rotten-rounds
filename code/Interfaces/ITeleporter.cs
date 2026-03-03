using Sandbox;

public interface ITeleporter
{
	bool LinkStarted { get; set; }
	void LinkTeleporter( Teleporter teleporter );

	void ActivateTeleporter( Teleporter teleporter );
}
