using Sandbox;
using System;


public sealed class PackAPunch : Component, IInteraction, IPower
{
	public bool Hold { get; set; } = false;
	public float HoldTime { get; set; } = 0f;

	public GameObject GO => this.GameObject;
	public bool Interactable { get; set; } = true;

	[Property]
	private bool NeedsPower { get; set; }

	[Property]
	private Collider InteractionTrigger { get; set; }

	[Property]
	private int Cost { get; set; } = 5000;


	private Random ran = new Random();


	[Sync]
	public bool Powered { get; set; } = false;


	public TimeSince TimeSincePacked { get; set; }


	public enum JackPotRewards
	{
		NoReload, //doesnt need to reload
		Headshotter, //every hit is considered a headshot
		MorePoints, //more points



	}

	protected override void OnUpdate()
	{

	}


	public void OnInteract(Player player)
	{
		if(!Powered) return;

		if ( Powered )
		{
			Roll();
		}
	}

	[Rpc.Host]
	private void UpgradeGun( string upgrade )
	{
		Log.Info("upgrade: " +  upgrade );
		switch ( upgrade )
		{
			case "x1":
				break;
			case "x2":
				break;
			case "x3":
				break;
			case "jackpot":
				break;
		}
	}

	[Rpc.Host]
	public void Roll()
	{
		string[] upgrade = { "x1", "x2", "x3", "jackpot" };
		double[] weights = { 0.5, 0.30, 0.15, 0.05 };

		string selectedupgrade;

		double total = weights.Sum();

		double r = ran.NextDouble() * total;

		double cumulative = 0;

		for ( int i = 0; i < upgrade.Length; i++ )
		{
			cumulative += weights[i];
			if ( r < cumulative )
			{
				selectedupgrade = upgrade[i];
				UpgradeGun( selectedupgrade );
				return;
			}
		}
		selectedupgrade = upgrade[^1];
		UpgradeGun( selectedupgrade );
		return;





	}




	[Rpc.Host]
	private void InteractionCheck(Player player)
	{
		if ( !Interactable ) return;

		if ( !player.CheckPointsInteraction(Cost) )
		{
			player.RemovePoints( Cost );
		}
		if( player.CheckPointsInteraction( Cost ) ) 
		{
			OnInteractionFailed( player, IInteraction.InteractionFReason.NoMoney );
		}
	}

	public void OnInteractionFailed(Player player, IInteraction.InteractionFReason reason)
	{
		
	}

	[Rpc.Host]
	private void TurnPowerOn()
	{
		Powered = true;
	}

	public void OnPowerTurnedOn()
	{
		TurnPowerOn();
	}

}
