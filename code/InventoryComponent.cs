using Sandbox;
using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.Marshalling;

public sealed class InventoryComponent : Component, IInventoryInterface
{

	[Property]
	Player Player { get; set; }






	[Sync] public NetList<WeaponStuff> Weapons { get; set; } = new();

	//currentmag | ammo total
	[Sync] public NetList<WeaponAmmo> WeaponsAmmo { get; set; } = new();


	public struct WeaponStuff
	{
		public string Weapon { get; set; }

		public int Packed { get; set; }
	}


	public struct WeaponAmmo
	{

		public int CurrentMag { get; set; }


		public int MagMax { get; set; }


		public int AmmoTotal { get; set; }


		public int MaxAmmoTotal { get; set; }
	}


	[Rpc.Host]
	public void AddWeapon( string weapon, int packed)
	{

		if ( this.Weapons.Count < 2 && !Player.Upgrading)
		{
			this.Weapons.Add( new WeaponStuff { Weapon = weapon, Packed = packed } );
			ClientAddWeapon( weapon, packed );
			var temp = GameObject.Clone( weapon );
			//Log.Info( "is it not giving" );
			GiveAmmo( 1, temp.GetComponentInChildren<BaseWeapon>().AmmoMax, temp.GetComponentInChildren<BaseWeapon>().MagMax );
			temp.Destroy();
			this.Player.ChangeCurrentSlot( Weapons.Count - 1 );

		}
		else if ( this.Weapons.Count >= 2 || Player.Upgrading )
		{
			var slot = this.Player.CurrentWeaponSlot;
			this.Weapons[slot] = new WeaponStuff { Weapon = weapon, Packed = packed };
			ClientAddWeaponSlot( weapon, slot, packed );
			var temp = GameObject.Clone( weapon );
			//Log.Info( "is it not giving" );
			GiveAmmo( slot, temp.GetComponentInChildren<BaseWeapon>().AmmoMax, temp.GetComponentInChildren<BaseWeapon>().MagMax );
			temp.Destroy();
			this.Player.ChangeCurrentSlot( slot );
		}
	}

	[Rpc.Owner]
	public void ClientAddWeapon( string weapon, int packed)
	{
		if ( Networking.IsHost ) return;
		this.Weapons.Add( new WeaponStuff { Weapon = weapon, Packed = packed } );
		this.Player.ChangeCurrentSlot( Weapons.Count - 1 );
	}

	[Rpc.Owner]
	public void ClientAddWeaponSlot( string weapon, int slot, int packed )
	{
		if ( Networking.IsHost ) return;
		//Log.Info( "weapon: " + weapon + " slot: " + slot );
		var idek = this.Weapons[slot];
		idek = new WeaponStuff { Weapon = weapon, Packed = packed };
		this.Weapons[slot] = idek;
		this.Player.ChangeCurrentSlot( slot );
	}

	//[Rpc.Owner]
	[Rpc.Host]
	public void RemoveWeapon( int slot )
	{
		var weaps = Weapons;
		weaps.RemoveAt( slot );
		Weapons = weaps;
		ClientRemoveWeapon( slot );
	}

	[Rpc.Owner]
	public void ClientRemoveWeapon( int slot )
	{
		if ( Networking.IsHost ) return;
		var weaps = Weapons;
		weaps.RemoveAt( slot );
		Weapons = weaps;
	}


	[Rpc.Host]
	public void GiveAmmo( int slot, int amount, int magMax )
	{
		if ( WeaponsAmmo.Count < 2 )
		{
			this.WeaponsAmmo.Add( new WeaponAmmo() { CurrentMag = magMax, AmmoTotal = amount, MagMax = magMax, MaxAmmoTotal = amount } );
			ClientGiveAmmo( amount, magMax );
			//Log.Info( WeaponsAmmo[0].CurrentMag );
		}
		else if ( WeaponsAmmo.Count >= 2 )
		{
			this.WeaponsAmmo[slot] = new WeaponAmmo() { CurrentMag = magMax, AmmoTotal = amount, MagMax = magMax, MaxAmmoTotal = amount };
			ClientGiveAmmoSlot( slot, amount, magMax );
		}
	}

	[Rpc.Owner]
	public void ClientGiveAmmo( int amount, int magMax )
	{
		if ( Networking.IsHost ) return;
		this.WeaponsAmmo.Add( new WeaponAmmo() { CurrentMag = magMax, AmmoTotal = amount, MagMax = magMax, MaxAmmoTotal = amount } );
	}

	[Rpc.Owner]
	public void ClientGiveAmmoSlot( int slot, int amount, int magMax )
	{
		if ( Networking.IsHost ) return;
		var x = this.WeaponsAmmo;
		x[slot] = new WeaponAmmo() { CurrentMag = magMax, AmmoTotal = amount, MagMax = magMax, MaxAmmoTotal = amount };
		this.WeaponsAmmo = x;
	}

	//[Rpc.Owner]
	[Rpc.Host]
	public void ReduceCurrentMag( int amount )
	{
		var ammo = this.WeaponsAmmo[this.Player.CurrentWeaponSlot];
		//Log.Info( ammo.CurrentMag );
		ammo.CurrentMag -= amount;
		//Log.Info( ammo.CurrentMag );
		this.WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
		ClientReduceCurrentMag( amount, this.Player.CurrentWeaponSlot );
	}

	[Rpc.Owner]
	public void ClientReduceCurrentMag( int amount, int ammoslot )
	{
		if ( Networking.IsHost ) return;
		var ammo = this.WeaponsAmmo[this.Player.CurrentWeaponSlot];
		//Log.Info( this.WeaponsAmmo );
		ammo.CurrentMag -= amount;
		this.WeaponsAmmo[this.Player.CurrentWeaponSlot] = ammo;
	}

	//[Rpc.Owner]
	[Rpc.Host]
	public void ReduceAmmo( int amount )
	{

		var ammo = WeaponsAmmo[this.Player.CurrentWeaponSlot];

		ammo.AmmoTotal = Math.Clamp( ammo.AmmoTotal - amount, 0, ammo.AmmoTotal );
		//Log.Info( ammo.AmmoTotal );
		WeaponsAmmo[this.Player.CurrentWeaponSlot] = ammo;
		ClientReduceAmmo( ammo.AmmoTotal, this.Player.CurrentWeaponSlot );
	}

	[Rpc.Owner]
	public void ClientReduceAmmo( int amount, int slot )
	{
		if ( Networking.IsHost ) return;
		//Log.Info( amount );
		var ammo = WeaponsAmmo[this.Player.CurrentWeaponSlot];

		ammo.AmmoTotal = amount;
		//Log.Info( ammo.AmmoTotal );
		WeaponsAmmo[this.Player.CurrentWeaponSlot] = ammo;
	}



	//[Rpc.Owner]
	[Rpc.Host]
	public void AddAmmo( int amount, int slot )
	{
		var ammo = WeaponsAmmo[slot];
		//Log.Info( ammo.AmmoTotal );
		ammo.AmmoTotal += amount;
		//Log.Info( ammo.AmmoTotal );
		WeaponsAmmo[slot] = ammo;
		ClientAddAmmo( amount, slot );
	}

	[Rpc.Owner]
	public void ClientAddAmmo( int amount, int slot )
	{
		if ( Networking.IsHost ) return;
		var ammo = WeaponsAmmo[slot];
		ammo.AmmoTotal += amount;
		WeaponsAmmo[slot] = ammo;
	}

	//[Rpc.Owner]
	[Rpc.Host]
	public void AddAmmoToMag( int amount )
	{

		var ammo = WeaponsAmmo[Player.CurrentWeaponSlot];

		if ( ammo.AmmoTotal <= 0 )
			return;


		int spaceLeft = ammo.MagMax - ammo.CurrentMag;
		if ( spaceLeft <= 0 )
			return;

		//Log.Info( ammo.CurrentMag );
		int ammoToMove = Math.Min( amount, Math.Min( spaceLeft, ammo.AmmoTotal ) );

		ammo.CurrentMag += ammoToMove;

		//Log.Info( ammo.CurrentMag );
		WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
		ClientAddAmmoToMag( ammoToMove, Player.CurrentWeaponSlot );


	}

	[Rpc.Owner]
	public void ClientAddAmmoToMag( int amount, int slot )
	{
		if ( Networking.IsHost ) return;
		var ammo = WeaponsAmmo[Player.CurrentWeaponSlot];
		ammo.CurrentMag += amount;
		WeaponsAmmo[Player.CurrentWeaponSlot] = ammo;
	}


	[Rpc.Owner]
	public void ClientGiveMaxAmmo()
	{
		if ( Networking.IsHost ) return;
		for ( int ammoslot = 0; ammoslot < WeaponsAmmo.Count; ammoslot++ )
		{
			//Log.Info( "setting ammo to " + WeaponsAmmo[ammoslot].MaxAmmoTotal + " in slot " + ammoslot );
			var x = WeaponsAmmo[ammoslot];
			x.AmmoTotal = WeaponsAmmo[ammoslot].MaxAmmoTotal;
			x.CurrentMag = WeaponsAmmo[ammoslot].MagMax;
			WeaponsAmmo[ammoslot] = x;
		}
	}

	[Rpc.Host]
	public void ServerGiveMaxAmmo()
	{
		for ( int ammoslot = 0; ammoslot < WeaponsAmmo.Count; ammoslot++ )
		{
			//Log.Info( "setting ammo to " + WeaponsAmmo[ammoslot].MaxAmmoTotal + " in slot " + ammoslot );
			var x = WeaponsAmmo[ammoslot];
			x.AmmoTotal = WeaponsAmmo[ammoslot].MaxAmmoTotal;
			x.CurrentMag = WeaponsAmmo[ammoslot].MagMax;
			WeaponsAmmo[ammoslot] = x;
		}
		ClientGiveMaxAmmo();
	}



	[Rpc.Host]
	public void ServerGiveCertainMaxAmmo( int slot )
	{
		var x = WeaponsAmmo[slot];
		x.AmmoTotal = WeaponsAmmo[slot].MaxAmmoTotal;
		x.CurrentMag = WeaponsAmmo[slot].MagMax;
		WeaponsAmmo[slot] = x;
	}

	[Rpc.Owner]
	public void ClientGiveCertainMaxAmmo( int slot )
	{
		if ( Networking.IsHost ) return;
		var x = WeaponsAmmo[slot];
		x.AmmoTotal = WeaponsAmmo[slot].MaxAmmoTotal;
		x.CurrentMag = WeaponsAmmo[slot].MagMax;
		WeaponsAmmo[slot] = x;
	}


	protected override void OnUpdate()
	{
		//Log.Info(Weapons.Count);
		if ( IsProxy ) return;
		if ( Weapons.Count <= 0 )
		{
			var weaponmanager = Scene.GetAllComponents<WeaponManager>().FirstOrDefault();
			weaponmanager.GivePlayerWeapon( "usp-rottenrounds.prefab", Player, 0, 0 );
		}
	}
}
