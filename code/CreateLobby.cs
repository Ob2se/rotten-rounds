using Microsoft.VisualBasic;
using Sandbox;
using Sandbox.Audio;
using Sandbox.ModelEditor;
using Sandbox.Services;
using System;
using System.Threading;
using System.Threading.Tasks;


public sealed class CreateLobby : Component, Component.INetworkListener
{
	[Sync] public List<MapData> MapList { get; set; } = new();
	public List<string> WeaponPrefabList { get; set; } = new();
	public List<string> WeaponIndents { get; set; } = new();

	[Sync] public MapData SelectedMap { get; set; }

	private SceneFile mapToLaunchScene;

	[Property]
	private List<GameObject> playerSpots = new();


	
	private Dictionary<long, GameObject> disabledSpots = new();

	private Dictionary<long, bool> ClientMapDownloadDone { get; set; } = new();
	private Dictionary<long, bool> ClientWeaponDownloadDone { get; set; } = new();

	private bool LaunchingMap = false;

	public bool isHost => Networking.IsHost;

	public List<Connection> Connections;

	private const string CacheFile = "map_cache.json";

	[Property]
	public GameObject lobbyPrefab { get; set; }



	public List<PlayerStats> playerStats = new();

	public struct PlayerStats
	{
		public string Level;
		public string Playcard;
		public long SteamId;

	}


	protected override void OnStart()
	{
		base.OnStart();

		if ( !IsProxy )
		{
			_ = LoadMapsAndWeapons();

			var stater = Sandbox.Services.Stats.GetLocalPlayerStats( "clickhq.rottenrounds" );

			//_ = CheckIfFirstTimePlaying();

		}

		if ( Networking.IsHost )
		{
			if ( MapList.Count > 0 )
			{
				SelectedMap = MapList.FirstOrDefault();
			}
			// initialize download trackers for host
			foreach ( var conn in Connection.All )
			{
				ClientMapDownloadDone[conn.SteamId] = false;
				ClientWeaponDownloadDone[conn.SteamId] = false;
			}
		}
	}

	private async Task CheckIfFirstTimePlaying()
	{
		var stater = Sandbox.Services.Stats.GetLocalPlayerStats( "clickhq.rottenrounds" );

		await stater.Refresh();

		var level = stater.Get( "level" );
		if ( level.Value == 0 )
		{
			Sandbox.Services.Stats.SetValue( "level", 1 );
		}

		var xp = stater.Get( "experience" );
		if ( xp.Value == 0 )
		{
			Sandbox.Services.Stats.SetValue( "experience", 1 );
		}
	}


	[Rpc.Broadcast]
	public void TellAllToGrabStats()
	{
		_ = GetStats();
	}


	
	public async Task GetStats()
	{
		foreach ( var conn in Connection.All )
		{
			playerStats.Clear();

			var stats = Sandbox.Services.Stats.GetPlayerStats( "clickhq.rottenrounds", conn.SteamId );

			await stats.Refresh();

			playerStats.Add( new PlayerStats
			{
				Level = stats.Get( "level" ).ToString(),
				SteamId = conn.SteamId,
				Playcard = "textures/playercards/default/_5ozi0c3w5ivq2k3wzti7_0.png"
			} );
		}
	}



	[Rpc.Host]
	private void DeleteAllPlayers()
	{
		foreach ( var spot in playerSpots )
		{
			Log.Info( "destroy" );
			spot.Destroy();
		}
	}


	[Rpc.Broadcast]
	private void GiveOwnershipToHost()
	{
		foreach ( var spot in playerSpots )
		{
			
			spot.Network.AssignOwnership( Connection.Host );
		}
	}

	

	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );

		

		foreach ( var spot in playerSpots ) 
		{
			if ( !spot.Enabled )
			{
				
				var spotter = lobbyPrefab.Clone(spot.WorldTransform);
				//spotter.Enabled = true;
				//spotter.GetComponentInChildren<MainMenuCharDresser>().BroadcastSetClothing();
				spotter.NetworkSpawn( channel );
				playerSpots.Remove( spot );
				disabledSpots.Add( channel.SteamId, spot );
				break;
			}
		}


		TellAllToGrabStats();

		

	}


	public void OnDisconnected(Connection channel)
	{
		if ( disabledSpots.TryGetValue( channel.SteamId, out var spot ) )
		{
			playerSpots.Add( spot );
			disabledSpots.Remove( channel.SteamId );
			foreach(var x in playerSpots)
			{
				Log.Info( x );
			}
		}

		if ( Networking.IsHost )
		{
			if ( ClientMapDownloadDone.ContainsKey( channel.SteamId ) )
				ClientMapDownloadDone.Remove( channel.SteamId );

			if ( ClientWeaponDownloadDone.ContainsKey( channel.SteamId ) )
				ClientWeaponDownloadDone.Remove( channel.SteamId );

			CheckIfAllClientsDownloaded();
		}
	}


	private async Task LoadMapsAndWeapons()
	{
		await GetMaps();
		await GetWeaponList();
		Log.Info( "Client loaded map and weapon metadata." );
	}

	private async Task GetMaps()
	{
		var cache = LoadCache();
		var cacheMap = cache.ToDictionary( m => m.FullIndent );

		MapList.Clear();
		if ( cache.Count > 0 )
		{
			MapList.AddRange( cache.OrderByDescending( m => m.UpVotes ) );
			SelectedMap = MapList.FirstOrDefault();
		}

		var search = await Package.FindAsync( "type:map" );

		// Determine what needs fetching:
		// - not in cache at all
		// - in cache but LastUpdate on server is newer than what we stored
		var toFetch = search.Packages
			.Where( p => !cacheMap.TryGetValue( p.FullIdent, out var cached )
					 || p.Updated > cached.LastUpdate )
			.ToList();

		if ( toFetch.Count > 0 )
		{
			var semaphore = new SemaphoreSlim( 10 );

			var tasks = toFetch.Select( async item =>
			{
				await semaphore.WaitAsync();
				try
				{
					var pkg = await Package.FetchAsync( item.FullIdent, false );
					var parent = pkg.GetMeta<string>( "ParentPackage" );
					if ( parent != "clickhq.rottenrounds" ) return null;

					return new MapData
					{
						Author = pkg.Org?.Title,
						Description = pkg.Summary ?? "No description :(", //pkg.Description seems to take the description with its html, should look into this for people who make fancy summaries
						DownVotes = pkg.VotesDown,
						UpVotes = pkg.VotesUp,
						Name = pkg.Title,
						Thumbnail = pkg.Thumb,
						Indent = pkg.Ident,
						FullIndent = pkg.FullIdent,
						LastUpdate = pkg.Updated
					};
				}
				catch { return null; }
				finally { semaphore.Release(); }
			} );

			var results = await Task.WhenAll( tasks );

			foreach ( var map in results.Where( m => m != null ) )
				cacheMap[map.FullIndent] = map; // insert or overwrite

			SaveCache( cacheMap.Values.ToList() );

			MapList.Clear();
			MapList.AddRange( cacheMap.Values.OrderByDescending( m => m.UpVotes ) );
			SelectedMap = MapList.FirstOrDefault();
		}
		else if ( cache.Count == 0 )
		{
			MapList.Clear();
			MapList.AddRange( cacheMap.Values.OrderByDescending( m => m.UpVotes ) );
			SelectedMap = MapList.FirstOrDefault();
		}
	}

	private List<MapData> LoadCache()
	{
		try
		{
			if ( Sandbox.FileSystem.Data.FileExists( CacheFile ) )
			{
				var json = Sandbox.FileSystem.Data.ReadAllText( CacheFile );
				return Json.Deserialize<List<MapData>>( json ) ?? new();
			}
		}
		catch { }
		SelectedMap = MapList.FirstOrDefault();
		return new();
	}

	private void SaveCache( List<MapData> maps )
	{
		try
		{
			Sandbox.FileSystem.Data.WriteAllText( CacheFile, Json.Serialize( maps ) );
		}
		catch { }
	}

	private async Task GetWeaponList()
	{
		try
		{

			var search = await Package.FindAsync( "game:clickhq.rottenrounds" );

			var tasks = search.Packages.Select( async item =>
			{
				try
				{
					var pkg = await Package.FetchAsync( item.FullIdent, false );
					Log.Info( pkg.Title );
					return item;
				}
				catch { }
				return null;
			} );




			var json = Sandbox.FileSystem.Mounted.ReadAllText( "resources/tempweaponlist.json" );
			var deser = Json.Deserialize<List<string>>( json );
			WeaponIndents.AddRange( deser );
		}
		catch { }
	}

	/// <summary>
	/// Host chooses a map, broadcast choice to all clients
	/// </summary>
	[Rpc.Host]
	public void ChooseMap( MapData map )
	{
		if ( !Networking.IsHost ) return;
		if ( map == null ) return;

		SelectedMap = map;
		ClientMapDownloadDone.Clear();
		ClientWeaponDownloadDone.Clear();

		foreach ( var conn in Connection.All )
		{
			ClientMapDownloadDone[conn.SteamId] = false;
			ClientWeaponDownloadDone[conn.SteamId] = false;
		}

		BroadcastMapChoice( map.FullIndent );
	}

	/// <summary>
	/// Broadcast the map choice to clients so they download it
	/// </summary>
	[Rpc.Broadcast]
	private void BroadcastMapChoice( string mapIndent )
	{
		_ = DownloadMap( mapIndent );
		_ = DownloadWeapons();
	}

	/// <summary>
	/// Download the map locally
	/// </summary>
	private async Task DownloadMap( string mapIndent )
	{
		var package = await Package.Fetch( mapIndent, false );
		if ( package == null ) return;

		await package.MountAsync();
		if ( !IsValid ) return;

		var scenePath = package.GetMeta( "PrimaryAsset", "" );
		ResourceLibrary.TryGet<SceneFile>( scenePath, out mapToLaunchScene );

		Log.Info( $"Client downloaded map {mapIndent}." );

		if ( !IsValid ) return;

		// notify host that this client finished map download
		DownloadFinishedMap( Connection.Local?.SteamId ?? 0 );
	}

	/// <summary>
	/// Download all weapons locally
	/// </summary>
	private async Task DownloadWeapons()
	{
		foreach ( var weaponIndent in WeaponIndents )
		{
			var package = await Package.Fetch( weaponIndent, false );
			if ( package == null ) continue;

			await package.MountAsync();
			if ( !IsValid ) return;

			var weaponPath = package.GetMeta( "PrimaryAsset", "" );
			WeaponPrefabList.Add( weaponPath );
		}

		Log.Info( "Client finished downloading weapons." );

		if ( !IsValid ) return;

		// notify host that this client finished weapon download
		DownloadFinishedWeapons( Connection.Local?.SteamId ?? 0 );
	}

	/// <summary>
	/// Called by client when map download finishes
	/// </summary>
	[Rpc.Host]
	private void DownloadFinishedMap( long steamId )
	{
		if ( !ClientMapDownloadDone.ContainsKey( steamId ) ) return;
		ClientMapDownloadDone[steamId] = true;

		CheckIfAllClientsDownloaded();
	}

	/// <summary>
	/// Called by client when weapons download finishes
	/// </summary>
	[Rpc.Host]
	private void DownloadFinishedWeapons( long steamId )
	{
		if ( !ClientWeaponDownloadDone.ContainsKey( steamId ) ) return;
		ClientWeaponDownloadDone[steamId] = true;

		CheckIfAllClientsDownloaded();
	}

	/// <summary>
	/// Host checks if everyone is done downloading
	/// </summary>
	[Rpc.Host]
	private void CheckIfAllClientsDownloaded()
	{
		if ( !Networking.IsHost ) return;

		// if anyone hasn't finished map or weapons, return
		if ( ClientMapDownloadDone.Values.Any( done => done == false ) ) return;
		if ( ClientWeaponDownloadDone.Values.Any( done => done == false ) ) return;

		// all clients finished
		LaunchingMap = true;

		Log.Info( "All clients finished downloading. Launching map..." );

		LoadMap();
	}

	/// <summary>
	/// Host changes the scene
	/// </summary>
	[Rpc.Broadcast]
	private void LoadMap()
	{
		if ( mapToLaunchScene == null )
		{
			Log.Warning( "No map downloaded to launch." );
			return;
		}

		GiveOwnershipToHost();

		if ( Networking.IsHost )
		{
			DeleteAllPlayers();
		}



		var options = new SceneLoadOptions();
		options.SetScene( mapToLaunchScene );

		ChangeServerName();

		Game.ChangeScene( options );

		// spawn managers if needed
		var sceneObject = Game.ActiveScene.CreateObject();
		sceneObject.NetworkMode = NetworkMode.Object;

		var modeManager = sceneObject.AddComponent<GameModeManager>();
		var weaponManager = sceneObject.AddComponent<WeaponManager>();
		weaponManager.WeaponPaths = WeaponPrefabList;

		sceneObject.NetworkSpawn();
	}

	[Rpc.Host]
	private void ChangeServerName()
	{
		Networking.ServerName = $"Rotten Rounds | In-Game | Round: 1";
	}


	// Launch the downloaded map on the client
	public void LaunchMap()
	{
		if ( mapToLaunchScene == null )
		{
			Log.Warning( "No map downloaded to launch." );
			return;
		}

		var options = new SceneLoadOptions();
		options.SetScene( mapToLaunchScene );

		Game.ChangeScene( options );

		var sceneObject = Game.ActiveScene.CreateObject();
		sceneObject.NetworkMode = NetworkMode.Object;

		var modeManager = sceneObject.AddComponent<GameModeManager>();
		var weaponManager = sceneObject.AddComponent<WeaponManager>();
		weaponManager.WeaponPaths = WeaponPrefabList;

		sceneObject.NetworkSpawn();

		Log.Info( "Client launched the downloaded map with weapons." );
	}

	/*[Sync]
	public List<MapData> mapList { get; set; } = new();

	public List<string> WeaponPrefabList { get; set; } = new();

	public List<string> WeaponIndents { get; set; } = new();

	private bool MapDownloaded { get; set; } = false;

	[Sync]
	SceneFile mapToLaunchScene { get; set; }

	private bool LaunchingMap { get; set; } = false;

	private Dictionary<long, bool> DownloadProgressMap { get; set; } = new();

	private Dictionary<long, bool> DownloadProgressWeapons { get; set; } = new();

	bool DownloadingMaps { get; set; } = false;
	bool DownloadingWeapons { get; set; } = false;

	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost ) 
		{
			_ = GetMaps();
			_ = GetWeaponAddons();
			GetWeapons();
		}
	}


	//should be similiar to GetMaps() but there is no supportgames for prefabs currently and addons are busted
	private void GetWeapons()
	{
		var json = Sandbox.FileSystem.Mounted.ReadAllText( "resources/tempweaponlist.json" );

		var deser = Json.Deserialize<List<string>>( json );

		foreach ( var x in deser )
		{
			WeaponIndents.Add( x );
		}
	}



	private async Task GetWeaponAddons()
	{

		var search = await Package.FindAsync( "type:prefab" );

		Log.Info( search.TotalCount + "found addons" );

		var tasks = search.Packages.Select( async item =>
		{
			try
			{
				var y = await Package.FetchAsync( item.FullIdent, false );
				
				var supports = y.GetMeta<string[]>( "GameSupport" ) ?? new string[0];

				if ( supports.Contains( "clickhq.rottenrounds" ) )
				{
					Log.Info( y.Title );
					return y;
				}

				
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to fetch {item.FullIdent}: {ex.Message}" );
			}

			return null; // skip invalid / unsupported packages
		} );

		var results = await Task.WhenAll( tasks );
		//mapList.AddRange( results.Where( m => m != null ) );

		//Log.Info( $"Preloaded {mapList.Count} RottenRounds maps." );


	}



	//get maps
	private async Task GetMaps()
	{

		mapList.Clear();

		var search = await Package.FindAsync( "type:map" );
		Log.Info(search.TotalCount + " maps found in total." );	
		var tasks = search.Packages.Select( async item =>
		{
			try
			{
				var y = await Package.FetchAsync( item.FullIdent, false );
				var supports = y.GetMeta<string[]>( "GameSupport" ) ?? new string[0];

				if ( supports.Contains( "clickhq.rottenrounds" ) )
				{
					return new MapData
					{
						Author = y.Org?.Title,
						Description = y.Description,
						DownVotes = y.VotesDown,
						UpVotes = y.VotesUp,
						Name = y.Title,
						Thumbnail = y.Thumb,
						Indent = y.Ident,
						FullIndent = y.FullIdent
					};
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to fetch {item.FullIdent}: {ex.Message}" );
			}

			return null; // skip invalid / unsupported packages
		} );

		var results = await Task.WhenAll( tasks );
		mapList.AddRange( results.Where( m => m != null ) );

		Log.Info( $"Preloaded {mapList.Count} RottenRounds maps." );








		*//*mapList.Clear();
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
						Log.Info( z );
					}
				}
			}
		}*//*
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
		if ( DownloadingWeapons || DownloadingMaps )
		{
			Log.Info( "clients are downloading" );
			return;
		}
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
		DownloadingMaps = true;
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
		DownloadingWeapons = true;
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

	}*/

	private async Task testing()
	{
		var search = await Package.FindAsync( "type:prefab" );

		foreach ( var item in search.Packages )
		{
			foreach ( var itemo in item.Tags )
			{
				
			}
		}

	}
	protected override void OnUpdate()
	{


		if ( Input.Pressed( "reload" ) )
		{
			_ = testing();
		}

		Connections = Connection.All.ToList();
	}
}
