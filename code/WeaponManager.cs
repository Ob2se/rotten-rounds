using Sandbox;
using System;
using static Sandbox.Clothing;
using System.Numerics;

public sealed class WeaponManager : Component
{
	[Sync( SyncFlags.FromHost )]
	public NetList<WeaponData> Weapons { get; set; } = new();

	protected override void OnStart()
	{
		base.OnStart();

		
		var json = FileSystem.Mounted.ReadAllText( "resources/weaponData.json" );

		var deser = Json.Deserialize<NetList<WeaponData>>( json );

		foreach ( var x in deser )
		{
			Weapons.Add( x );
		}
	}

	[Rpc.Broadcast]
	public void GivePlayerStartingWeapon(string WeaponName, Player player)
	{

		//if ( IsProxy ) return;
		
		foreach ( var x in Weapons )
			{
			if ( x.Name == WeaponName )
			{
				Log.Info( x.Name );
				Log.Info( Weapons.Count );
				Log.Info( player.Weapons.Count );
				
				//player.Weapons.Add( x );
				
				//Log.Info( "Gave " + player.PlayerConnection.DisplayName + " " + WeaponName );

			}
		}
		
	}


	protected override void OnUpdate()
	{

	}
}
