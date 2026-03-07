using Sandbox;
using Sandbox.Rendering;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
public sealed class WallBuy : Component, IInteraction
{


	[Property]
	public bool Hold { get; set; }

	public bool Interactable { get; set; } = true;

	[Property, Sync]
	public string WeaponIdent { get; set; }

	[Property]
	public int Cost { get; set; }

	[Property]
	public int AmmoCost { get; set; } = 500;

	public GameObject GO => this.GameObject;

	WeaponManager WeaponManager { get; set; }
	public float HoldTime => 0f;

	[Sync]
	public string WeaponName { get; set; }


	Random rand = new Random();

	[Sync]
	private string WeaponPath { get; set; }

	[Property]
	public Collider InteractionTriggerBox { get; set; }

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			if ( WeaponPath == null && WeaponIdent == null )
			{
				if ( WeaponManager != null )
				{
					var randomweap = rand.Next( WeaponManager.WeaponPaths.Count );
					if ( WeaponManager.WeaponPaths[randomweap] != null )
					{
						Log.Info( "how manuy times" );
						WeaponPath = WeaponManager.WeaponPaths[randomweap];
						var temp = GameObject.Clone( WeaponPath ).GetComponentInChildren<BaseWeapon>();
						if ( temp == null )
						{
							Log.Error( "WallBuy | SOMETHING SERIOUSLY WRONG!" );
						}

						WeaponName = temp.WeaponName;
						temp.GameObject.Destroy();
						var ui = this.GameObject.AddComponent<weaponbuy>();
						ui.WeaponName = WeaponName;
						Interactable = true;
					}
				}
			}
		}
	}


	protected override void OnStart()
	{

		InteractionTriggerBox.OnObjectTriggerEnter += OnInteractionEnter;
		InteractionTriggerBox.OnObjectTriggerExit += OnInteractionExit;

		WeaponManager = Scene.GetAll<WeaponManager>().FirstOrDefault();

		if ( WeaponIdent != null )
		{
			_ = DownloadWeapon();
		}

	}


	private void OnInteractionEnter( GameObject obj )
	{
		Log.Info( "hellur" );
		var player = obj.GetComponent<Player>();
		if ( player != null )
		{
			if ( Interactable )
			{
				player.CurrentInteraction = this;
			}

		}
	}

	private void OnInteractionExit( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if ( player != null )
		{
			player.CurrentInteraction = null;

		}
	}

	private async Task DownloadWeapon()
	{
		var package = await Package.Fetch( WeaponIdent, false );

		if ( package == null )
		{
			WeaponPath = WeaponManager.WeaponPaths[rand.Next( WeaponManager.WeaponPaths.Count )];
			return;
		}

		if ( package != null )
		{
			WeaponPath = package.GetMeta( "PrimaryAsset", "" );

			//Log.Info( "Downloaded weapon for wallbuy: " + WeaponPath );
		}

		var temp = GameObject.Clone( WeaponPath ).GetComponent<BaseWeapon>();
		if ( temp == null )
		{
			Log.Error( "WallBuy | SOMETHING SERIOUSLY WRONG!" );
		}

		WeaponName = temp.WeaponName;

		var ui = this.GameObject.AddComponent<weaponbuy>();
		ui.WeaponName = WeaponName;
		Interactable = true;

	}


	private void GiveWeapon( Player player )
	{

		player.Inventory.AddWeapon( WeaponPath, 0 );

	}




	private bool PriceCheck( Player player )
	{

		return true;
	}

	[Rpc.Host]
	private void BuyWeapon( Player player )
	{
		if ( Networking.IsHost )
		{
			if ( player.Points - Cost < 0 )
			{
				OnInteractionFailed( player, IInteraction.InteractionFReason.NoMoney );
				return;
			}

			player.RemovePoints( Cost );
			player.PlayChaChing();
			GiveWeapon( player );
		}
	}


	[Rpc.Host]
	private void GiveAmmo( Player player, int slot )
	{
		if ( player.Points - AmmoCost < 0 )
		{
			OnInteractionFailed( player, IInteraction.InteractionFReason.NoMoney );
			return;
		}
		Log.Info( "got ammo" );
		player.RemovePoints( AmmoCost );
		player.Inventory.ServerGiveCertainMaxAmmo( slot );


	}



	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		//could add some feedback here later
	}
	public void OnInteract( Player player )
	{
		var playerweapons = player.Inventory.Weapons;
		foreach ( var weapon in playerweapons )
		{
			if ( weapon.Weapon == WeaponPath )
			{
				var index = playerweapons.IndexOf( weapon );
				GiveAmmo( player, index );
				return;
			}
		}
		BuyWeapon( player );
	}



}
