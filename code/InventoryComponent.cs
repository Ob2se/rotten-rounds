using Sandbox;
using System;

public sealed class InventoryComponent : Component, IInventoryInterface
{

	[Property]
	Player Player { get; set; }

	public NetList<string> Weapons { get; set; } = new();

	//currentmag | ammo total
	public NetList<WeaponAmmo> WeaponsAmmo { get; set; } = new();

	public struct WeaponAmmo
	{
		public int CurrentMag { get; set; }

		public int MagMax { get; set; }

		public int AmmoTotal { get; set; }
	}

	[Rpc.Owner]
	public void AddWeapon(string weapon, int slot)
	{
		
		if(Weapons.Count < 2)
		{
			Weapons.Add(weapon);
			var temp = GameObject.Clone( weapon );
			Log.Info( "is it not giving" );
			GiveAmmo( slot, temp.GetComponentInChildren<BaseWeapon>().AmmoMax, temp.GetComponentInChildren<BaseWeapon>().MagMax );
			temp.Destroy();
			Player.ChangeCurrentSlot( Weapons.Count - 1 );

		}
		else if(Weapons.Count >= 2)
		{
			Weapons[slot] = weapon;
			var temp = GameObject.Clone( weapon );
			Log.Info( "is it not giving" );
			GiveAmmo( slot, temp.GetComponentInChildren<BaseWeapon>().AmmoMax, temp.GetComponentInChildren<BaseWeapon>().MagMax );
			temp.Destroy();
			Player.ChangeCurrentSlot( slot );
		}
	}

	[Rpc.Owner]
	public void RemoveWeapon(int slot)
	{
		Weapons.RemoveAt(slot);
	}

	[Rpc.Owner]
	public void GiveAmmo(int slot, int amount, int magMax)
	{
		if ( WeaponsAmmo.Count < 2 )
		{
			WeaponsAmmo.Add( new WeaponAmmo() { CurrentMag = magMax, AmmoTotal = amount, MagMax = magMax  } );
			Log.Info( WeaponsAmmo[0].CurrentMag );
		}
		else if ( WeaponsAmmo.Count >= 2 )
		{
			WeaponsAmmo[slot] = new WeaponAmmo() { CurrentMag = magMax, AmmoTotal = amount, MagMax = magMax };
		}
	}


	[Rpc.Owner]
	public void ReduceCurrentMag( int amount )
	{
		var ammo = WeaponsAmmo[Player.CurrentWeaponSlot];
		Log.Info( ammo.CurrentMag );
		ammo.CurrentMag -= amount;
		Log.Info( ammo.CurrentMag );
		WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
	}


	[Rpc.Owner]
	public void ReduceAmmo( int amount )
	{
		var ammo = WeaponsAmmo[Player.CurrentWeaponSlot];
		Log.Info( ammo.AmmoTotal );
		ammo.AmmoTotal = Math.Clamp(ammo.AmmoTotal - amount, 0 , ammo.AmmoTotal);
		Log.Info( ammo.AmmoTotal );
		WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
	}

	[Rpc.Owner]
	public void AddAmmo( int amount )
	{
		var ammo = WeaponsAmmo[Player.CurrentWeaponSlot];
		Log.Info( ammo.AmmoTotal );
		ammo.AmmoTotal += amount;
		Log.Info( ammo.AmmoTotal );
		WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
	}

	[Rpc.Owner]
	public void AddAmmoToMag( int amount )
	{

		var ammo = WeaponsAmmo[Player.CurrentWeaponSlot];
		
		if ( ammo.AmmoTotal <= 0 )
			return;


		int spaceLeft = ammo.MagMax - ammo.CurrentMag;
		if ( spaceLeft <= 0 )
			return;

		Log.Info( ammo.CurrentMag );
		int ammoToMove = Math.Min( amount, Math.Min( spaceLeft, ammo.AmmoTotal ) );

		ammo.CurrentMag += ammoToMove;
		
		Log.Info( ammo.CurrentMag );
		WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
		
		
	}


	protected override void OnUpdate()
	{
		//Log.Info(Weapons.Count);
	}
}
