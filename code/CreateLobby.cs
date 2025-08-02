using Microsoft.VisualBasic;
using Sandbox;
using Sandbox.Audio;
using Sandbox.ModelEditor;
using System.Threading.Tasks;

public sealed class CreateLobby : Component, Component.INetworkListener
{

	public List<MapData> mapList { get; set; } = new();

	[Sync]
	SceneFile mapToLaunchScene { get; set; }

	private bool LaunchingMap { get; set; } = false;

	private Dictionary<long, bool> DownloadProgress { get; set; } = new();

	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost ) 
		{ 
			_ = GetMaps();
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
			DownloadProgress.TryAdd( x.SteamId, false );
			Log.Info( DownloadProgress.First() );
		}

		TellClientsToStartDownload( SelectedMap.FullIndent );

		LaunchingMap = true;
		
	}

	[Rpc.Broadcast]
	private void LoadMap()
	{
		_ = InitializeMap();
		


	}


	private async Task InitializeMap()
	{
		var SceneOptions = new SceneLoadOptions();
		SceneOptions.SetScene( mapToLaunchScene );
		Game.ChangeScene( SceneOptions );
		Game.ActiveScene.CreateObject().AddComponent<GameModeManager>();
		//await Task.Frame();

		//x.Name = "GameModeManager";
		//x.AddComponent<GameModeManager>();
	}



	[Rpc.Host]
	private void CheckIfDownloaded( ) 
	{

		foreach(var x in DownloadProgress)
		{
			if(x.Value == false)
			{
				return;
			}
		}

		LoadMap();
		LaunchingMap = false;
	}



	[Rpc.Broadcast]
	private void TellClientsToStartDownload(string SelectedMapIndent) 
	{
		_ = FetchMap( SelectedMapIndent );
	}



	[Rpc.Host]
	private void DownloadFinished(long SteamId)
	{
		if ( DownloadProgress?[SteamId] == false )
		{
			DownloadProgress.Remove( SteamId );
			DownloadProgress.Add(SteamId, true );
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

		DownloadFinished( Connection.Local.SteamId );

		Log.Info( scenePath );
		return;
	}


	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
		{
			if ( LaunchingMap == true )
			{
				CheckIfDownloaded();
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
