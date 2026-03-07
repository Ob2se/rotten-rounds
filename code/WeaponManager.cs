using Sandbox;
using System;
using System.Numerics;
using System.Threading.Tasks;
using static Sandbox.Clothing;
using static Sandbox.PhysicsContact;

public sealed class WeaponManager : Component
{


	[Property, Sync( SyncFlags.FromHost )]
	public List<string> WeaponPaths { get; set; } = new();

	bool DownloadingWeapons = false;

	public string StartingWeapon { get; set; }


	protected override void OnStart()
	{
		base.OnStart();

	}


	[Rpc.Host]
	public void GivePlayerWeapon( string Weapon, Player player, int slot, int packed )
	{

		if ( WeaponPaths.Contains( Weapon ) )
		{
			player.Inventory.AddWeapon( Weapon, packed );
		}

	}

	private async Task DownloadWeapons( List<string> WeaponIndents )
	{
		foreach ( var weaponIndent in WeaponIndents )
		{
			var package = await Package.Fetch( weaponIndent, false );
			if ( package == null ) continue;

			await package.MountAsync();
			var weaponPath = package.GetMeta( "PrimaryAsset", "" );
			Log.Info( weaponPath );
			WeaponPaths.Add( weaponPath );
		}

		if ( WeaponPaths.Count <= 0 )
		{
			Log.Info( "failed to download weapons again! something majorly wrong!" );
			DownloadingWeapons = false;
			return;
		}

		Log.Info( "Client finished downloading weapons." );
	}



	protected override void OnUpdate()
	{
		if ( WeaponPaths.Count() <= 0 && !DownloadingWeapons )
		{
			DownloadingWeapons = true;
			var json = Sandbox.FileSystem.Mounted.ReadAllText( "resources/tempweaponlist.json" );
			var deser = Json.Deserialize<List<string>>( json );
			List<string> weapono = new();
			weapono.AddRange( deser );
			_ = DownloadWeapons( weapono );
		}
	}
}
