using Sandbox;
using System;


public sealed class PackAPunch : Component, IInteraction, IPower
{
	public bool Hold { get; set; } = false;
	public float HoldTime { get; set; } = 0f;

	public GameObject GO => this.GameObject;

	[Sync]
	public bool Interactable { get; set; } = true;

	[Property]
	private bool NeedsPower { get; set; }

	[Property]
	private Collider InteractionTrigger { get; set; }

	[Property]
	public int Cost { get; set; } = 5000;


	private Random ran = new Random();


	[Property]
	public Material x1Material { get; set; }

	[Property]
	public Material x2Material { get; set; }

	[Property]
	public Material x3Material { get; set; }

	[Property]
	public Material JackpotMaterial { get; set; }

	[Sync]
	public string WeaponBeingUpgraded { get; set; }

	[Sync]
	public Player playerUpgrading { get; set; }

	[Sync]
	public bool CanTakeWeapon { get; set; }


	[Sync]
	public int Upgrade { get; set; }

	[Sync]
	public bool Upgraded { get; set; }

	[Sync]
	public bool Upgrading { get; set; }

	public TimeSince TimeSinceUpgradeStarted { get; set; }

	[Sync]
	private GameObject tempObject { get; set; }


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
		if ( Networking.IsHost )
		{
			if ( Upgrading && TimeSinceUpgradeStarted >= 5f )
			{
				SpawnGun( WeaponBeingUpgraded, Upgrade );
				Upgrading = false;
				Upgraded = true;
			}

			if ( Upgraded && TimeSinceUpgradeStarted >= 10f )
			{
				playerUpgrading.Upgrading = false;
				GiveWeapon();
				DeleteTemp();
				
			}
		}
		



	}

	[Rpc.Host]
	private void TakeGun( Player player, int slot )
	{
		var curslot = player.CurrentWeaponSlot;
		WeaponBeingUpgraded = player.Inventory.Weapons[slot].Weapon;
		//player.Inventory.RemoveWeapon( slot );
		UpgradeGunStart(player);
	}


	[Rpc.Host]
	private void SpawnGun(string Weapon, int packed)
	{
		var weapPrefab = GameObject.GetPrefab( Weapon );
		
		switch ( packed )
		{
			case 1:
				weapPrefab.GetComponentInChildren<BaseWeapon>().WeaponModel.MaterialOverride = x1Material;
				break;
			case 2:
				weapPrefab.GetComponentInChildren<BaseWeapon>().WeaponModel.MaterialOverride = x2Material;
				break;
			case 3:
				weapPrefab.GetComponentInChildren<BaseWeapon>().WeaponModel.MaterialOverride = x3Material;
				break;
			case 4:
				weapPrefab.GetComponentInChildren<BaseWeapon>().WeaponModel.MaterialOverride = JackpotMaterial;
				break;

		}

		//weapPrefab.Clone(GameObject.WorldPosition + Vector3.Up * 10 );
		var weapgo = new GameObject();
		weapgo.WorldPosition = this.GameObject.WorldPosition + Vector3.Up * 25;
		var weap = weapgo.AddComponent<ModelRenderer>();
		weap.Model = weapPrefab.GetComponentInChildren<BaseWeapon>().WeaponModel.Model;
		weap.MaterialOverride = weapPrefab.GetComponentInChildren<BaseWeapon>().WeaponModel.GetMaterial();
		tempObject = weapgo;
		weapgo.NetworkSpawn();
		CanTakeWeapon = true;
		Interactable = true;
	}


	public void OnInteract(Player player)
	{
		if(!Powered) return;

		if ( Powered && !Upgrading && !Upgraded)
		{
			TakeGun( player, player.CurrentWeaponSlot );
			player.Upgrading = true;
			player.CurrentWeaponClass.WeaponModel.Set( "b_holster", true );
			Roll();
		}
		
		if ( Upgraded && CanTakeWeapon && playerUpgrading == player )
		{
			
			GiveWeapon();
			DeleteTemp();
			player.Upgrading = false;
		}

	}

	[Rpc.Broadcast]
	private void DeleteTemp()
	{
		tempObject.Destroy();
	}


	[Rpc.Host]
	private void GiveWeapon()
	{
		playerUpgrading.Inventory.AddWeapon( WeaponBeingUpgraded, Upgrade );
		
		Upgrading = false;
		playerUpgrading = null;
		Upgrade = 0;
		CanTakeWeapon = false;
		Upgraded = false;
	}


	[Rpc.Host]
	private void UpgradeGun( string upgrade )
	{

		switch ( upgrade )
		{
			case "x1":
				Upgrade = 1;
				break;
			case "x2":
				Upgrade = 2;
				break;
			case "x3":
				Upgrade = 3;
				break;
			case "jackpot":
				Upgrade = 4;
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
	private void UpgradeGunStart( Player player)
	{
		playerUpgrading = player;
		Upgrading = true;
		Interactable = false;
		TimeSinceUpgradeStarted = 0f;

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
