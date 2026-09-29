using Sandbox;
using System;

public sealed class EasterEggController : Component, IEasterEgg
{

	[Property, Sync]
	public NetList<InteractionEE> InteractableEE { get; set; } = new();

	[Property, Sync]
	public NetList<ShootableEE> ShootableEE { get; set; } = new();

	[Property, Group("Actions")]
	public event Action<GameObject> PieceComplete;


	[Property, Group( "Actions")]
	public event Action EasterEggComplete;

	public void OnPieceComplete(GameObject go, bool shootable)
	{
		if ( shootable )
		{
			if ( ShootableEE.Count > 0 )
			{
				foreach ( var ee in ShootableEE )
				{
					if ( ee.GameObject == go)
					{
						ShootableEE.Remove( ee );
						PieceComplete?.Invoke(go);
						CheckIfEasterEggIsDone();
						return;
					}
				}
			}
		}
		if ( !shootable )
		{
			if ( InteractableEE.Count > 0 )
			{
				
				foreach ( var ee in InteractableEE )
				{
					if ( ee.GameObject == go )
					{
						InteractableEE.Remove( ee );
						PieceComplete?.Invoke( go );
						CheckIfEasterEggIsDone();
						return;
					}
				}
			}
		}
	}

	[Rpc.Host]
	public void CheckIfEasterEggIsDone()
	{
		if ( InteractableEE.Count > 0 )
		{
			return;
		}
		if ( ShootableEE.Count > 0 )
		{
			return;
		}

		EasterEggComplete.Invoke();

	}

	protected override void OnUpdate()
	{

	}
}
