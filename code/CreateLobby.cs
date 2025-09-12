using Microsoft.VisualBasic;
using Sandbox;
using Sandbox.Audio;
using Sandbox.ModelEditor;
using System.Threading.Tasks;


public sealed class CreateLobby : Component, Component.INetworkListener
{

	[Sync]
	public List<MapData> mapList { get; set; } = new();

	public List<string> WeaponPrefabList { get; set; } = new();

	public List<string> WeaponIndents { get; set; } = new();

	private bool MapDownloaded { get; set; } = false;

	[Sync]
	SceneFile mapToLaunchScene { get; set; }

	private bool LaunchingMap { get; set; } = false;

	private Dictionary<long, bool> DownloadProgressMap { get; set; } = new();

	private Dictionary<long, bool> DownloadProgressWeapons { get; set; } = new();

	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost ) 
		{ 
			_ = GetMaps();
			GetWeapons();
		}
	}


	//should be similiar to GetMaps() but there is no supportgames for prefabs currently and addons are busted
	private void GetWeapons()
	{
		var json = FileSystem.Mounted.ReadAllText( "resources/tempweaponlist.json" );

		var deser = Json.Deserialize<List<string>>( json );

		foreach ( var x in deser )
		{
			WeaponIndents.Add( x );
		}
	}





	//get maps
	private async Task GetMaps()
	{
		mapList.Clear();
		var idek = await Package.FindAsync( "(rr" );
		foreach ( var item in idek.Packages )
		{
			Log.Info( item.Title );
			if ( item.TypeName == "map" )
			{
				var y = await Package.FetchAsync( item.FullIdent, false );
				var x = y.GetMeta<string[]>("GameSupport");

				Log.Info( y.GetMeta<string[]>( "GameSupport" ).First() );

				foreach(var z in x)
				{
					if ( z == "clickhq.rottenrounds" )
					{
						MapData mapData = new MapData();
						mapData.Author = y.Org.Title;
						mapData.Description = y.Description;
						mapData.DownVotes = y.VotesDown;
						mapData.UpVotes = y.VotesUp;
						mapData.Name = y.Title;
						mapData.Thumbnail = y.Thumb;
						mapData.Indent = y.Ident;
						mapData.FullIndent = y.FullIdent;
						mapList.Add( mapData );

					}
				}
			}
		}
	}


	[Rpc.Host]
	public void StartMap(MapData SelectedMap)
	{
		Log.Info( "wtf" );
		foreach ( var x in Connection.All )
		{
			DownloadProgressMap.TryAdd( x.SteamId, false );
			Log.Info( DownloadProgressMap.First() );
		}

		TellClientsToStartDownload( SelectedMap.FullIndent );

		LaunchingMap = true;
		
	}

	[Rpc.Broadcast]
	private void LoadMap()
	{
		InitializeMap();
		


	}


	private void InitializeMap()
	{
		var SceneOptions = new SceneLoadOptions();
		SceneOptions.SetScene( mapToLaunchScene );
		Game.ChangeScene( SceneOptions );
		var x = Game.ActiveScene.CreateObject();
		x.NetworkMode = NetworkMode.Object;
		var y = x.AddComponent<GameModeManager>();
		var z = x.AddComponent<WeaponManager>();
		z.WeaponPaths = WeaponPrefabList;
		x.NetworkSpawn();

	}



	[Rpc.Host]
	private void CheckIfMapDownloaded( ) 
	{

		foreach(var x in DownloadProgressMap)
		{
			if(x.Value == false)
			{
				return;
			}
		}

		MapDownloaded = true;
		StartWeaponDownload();
	}



	[Rpc.Host]
	private void CheckIfWeaponsDownloaded()
	{

		foreach ( var x in DownloadProgressWeapons )
		{
			if ( x.Value == false )
			{
				return;
			}
			Log.Info( x );
		}

		LoadMap();
	}


	[Rpc.Broadcast]
	private void TellClientsToStartDownload(string SelectedMapIndent) 
	{
		_ = FetchMap( SelectedMapIndent );
	}



	[Rpc.Host]
	private void DownloadFinishedMap(long SteamId)
	{
		if ( DownloadProgressMap?[SteamId] == false )
		{
			DownloadProgressMap.Remove( SteamId );
			DownloadProgressMap.Add(SteamId, true );
		}
		
	}


	[Rpc.Host]
	private void DownloadFinishedWeapons( long SteamId )
	{
		if ( DownloadProgressWeapons?[SteamId] == false )
		{
			DownloadProgressWeapons.Remove( SteamId );
			DownloadProgressWeapons.Add( SteamId, true );
		}

	}



	private async Task FetchMap( string SelectedMapIndent )
	{
		var package = await Package.Fetch( SelectedMapIndent, false );
		if ( package == null ) 
		{ 
			return; 
		}

		await package.MountAsync();

		var scenePath = package.GetMeta( "PrimaryAsset", "" );

		SceneFile SceneFile;

		var scene = ResourceLibrary.TryGet<SceneFile>( scenePath , out SceneFile );

		Log.Info( SceneFile );

		mapToLaunchScene = SceneFile;

		DownloadFinishedMap( Connection.Local.SteamId );

		Log.Info( scenePath );
		return;
	}


	[Rpc.Host]
	private void StartWeaponDownload()
	{
		foreach ( var x in Connection.All )
		{
			DownloadProgressWeapons.TryAdd( x.SteamId, false );
		}

		TellClientToDownloadWeapons();
	}



	[Rpc.Broadcast]
	private void TellClientToDownloadWeapons()
	{
		_ = DownloadWeapons();
	}


	private async Task DownloadWeapons()
	{
		Log.Info( "weapon download started" );
		foreach ( var x in WeaponIndents )
		{
			var weapon = await Package.Fetch( x, false );

			if ( weapon == null )
			{
				return;
			}

			await weapon.MountAsync();

			var weaponPath = weapon.GetMeta( "PrimaryAsset", "" );
			WeaponPrefabList.Add( weaponPath );

			Log.Info( weaponPath + " was added to list" );
			
		}

		DownloadFinishedWeapons( Connection.Local.SteamId );

	}


	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			if ( LaunchingMap )
			{
				CheckIfMapDownloaded();
				if( MapDownloaded )
				{
					CheckIfWeaponsDownloaded();
				}
			}

		}
		





		if ( Input.Pressed( "reload" ) )
		{
			foreach ( var item in mapList )
			{
				Log.Info( item.Author );
			}
		}


	}
}
