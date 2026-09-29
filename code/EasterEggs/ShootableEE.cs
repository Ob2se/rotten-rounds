using Sandbox;

public sealed class ShootableEE : Component, IEasterEggPiece
{

	[Property]
	public bool RequireLuckyLeaded { get; set; } = false;

	[Property]
	public float Health { get; set; } = 1f;



	public void OnPiece()
	{
		Scene.RunEvent<IEasterEgg>(x => x.OnPieceComplete(this.GameObject, true));
		this.GameObject.Destroy();
	}

	protected override void OnUpdate()
	{

	}
}
