using Sandbox;
using Sandbox.Network;
using System;
using System.Threading.Tasks;

public sealed class TestingMode : Component, Component.INetworkListener, IZombieHandler
{
	[Sync( SyncFlags.FromHost ), Change( "RoundChanged" )] public int Round { get; set; }

	[Sync, Change( "PlayerListChange" )] public List<PlayerListInfo> PlayersList { get; set; } = new();

	[Sync] int zombieCount { get; set; }

	[Sync] int maxZombies { get; set; }

	int zombiesLeft;

	private bool PlayersConnecting { get; set; }

	Random rndm = new Random();


	GameObject spawnPoint;

	bool RoundChange = false;



	[Property]
	private bool editortesting { get; set; } = false;

	public static event Action PlayerListChange;

	public static event Action ChangeRound;




	public bool Restarting { get; set; } = false;

	private float CurrentZombiesHealth = 50f;

	[Sync]
	private TimeSince TimeSinceGameStarted { get; } = 0;


	private TimeSince TimeSinceInstaKillStarted = 0;
	private TimeSince TimeSinceFireSaleStarted = 0;
	private TimeSince TimeSinceDoublePointsStarted = 0;

	private float InstaKillTime = 30f;
	private float FireSaleTime = 30f;
	private float DoublePointsTime = 30f;

	private bool InstaKillStarted = false;
	private bool FireSaleStarted = false;
	private bool DoublePointsStarted = false;

	WeaponManager WeaponManager { get; set; }

	[Sync][Property] public GameObject zombiePrefab { get; set; }


	//taken from network helper example

	/// <summary>
	/// Create a server (if we're not joining one)
	/// </summary>
	[Property] public bool StartServer { get; set; } = true;

	/// <summary>
	/// The prefab to spawn for the player to control.
	/// </summary>
	[Property] public GameObject PlayerPrefab { get; set; }

	/// <summary>
	/// A list of points to choose from randomly to spawn the player in. If not set, we'll spawn at the
	/// location of the NetworkHelper object.
	/// </summary>
	[Property] public List<GameObject> SpawnPoints { get; set; } = new();

	[Property] public List<ZombieSpawn> ZombieSpawnPoints { get; set; } = new();


	private List<MysteryBox> FireSaleBoxes = new();

	public List<Mysteryboxplacement> mysteryboxplacments { get; set; } = new();

	private bool MaxZombiesAtOnce = false;
	private bool AllZombiesSpawned = false;

	private int ZombiesSpawned = 0;

	[Sync]
	private bool GameStarted { get; set; } = false;

	[Sync]
	public bool PowerOn { get; set; } = false;


	[Change( "PlayerListChanged" )] public Dictionary<Connection, Player> PlayerList { get; set; } = new();





	CloneConfig ZombieConfig = new CloneConfig();

	TimeSince TimeSinceLastZombie = 3f;

	protected override void OnStart()
	{
		PlayerPrefab = GameObject.GetPrefab( "playercharacter.prefab" );
		zombiePrefab = GameObject.GetPrefab( "zombie.prefab" );

		base.OnStart();

		if ( !Networking.IsActive )
		{
			LobbyConfig testlob = new LobbyConfig
			{
				Name = "RottenRounds-Testing",
				MaxPlayers = 10,
			};

			Networking.CreateLobby( testlob );

			//await Task.DelayRealtimeSeconds( 1 );

			/*SceneFile lobbyScene;

			ResourceLibrary.TryGet<SceneFile>( "scenes/lobby.scene", out lobbyScene );
			Scene.Load( lobbyScene );*/
		}

		var x = Scene.GetAllComponents<PlayerSpawn>();
		foreach ( var i in x )
		{
			SpawnPoints.Add( i.GameObject );
		}


		var y = Scene.GetAllComponents<Mysteryboxplacement>();
		foreach ( var mplace in y )
		{
			mysteryboxplacments.Add( mplace );
			if ( mplace.StartWithThisPlacement )
			{
				SpawnMysterbox( mplace );
			}
		}


		var z = Scene.GetAllComponents<ZombieSpawn>();
		foreach ( var zspawn in z )
		{
			Log.Info( zspawn + " a spawn!" );
			ZombieSpawnPoints.Add( zspawn );
		}

		GetWeaponManager();
		//GameStarted = true;
	}




	[Rpc.Host]
	private void SpawnMysterbox( Mysteryboxplacement mysbox )
	{
		var mbox = GameObject.GetPrefab( mysbox.Mysterybox.ResourcePath );
		var mboxSpawn = mbox.Clone();
		mboxSpawn.WorldTransform = mysbox.MysteryboxPlacement.WorldTransform;
		mboxSpawn.GetComponent<MysteryBox>().Interactable = true;
		mboxSpawn.NetworkSpawn();
	}


	[Rpc.Host]
	public void ChoseNewMBSpot()
	{
		Random ran = new Random();
		var randomplace = mysteryboxplacments[ran.Next( mysteryboxplacments.Count )];
		SpawnMysterbox( randomplace );
	}


	[Rpc.Host]
	public void StartFireSale()
	{
		FireSaleBoxes.Clear();
		TimeSinceFireSaleStarted = 0;
		FireSaleStarted = true;
		foreach ( var x in mysteryboxplacments )
		{
			if ( !x.activated )
			{
				var mbox = GameObject.GetPrefab( x.Mysterybox.ResourcePath );
				var mboxSpawn = mbox.Clone();
				mboxSpawn.WorldTransform = x.MysteryboxPlacement.WorldTransform;
				mboxSpawn.NetworkSpawn();
				var y = mboxSpawn.GetComponent<MysteryBox>();
				y.Cost = 10;
				y.Firesalebox = true;
				FireSaleBoxes.Add( y );
			}
		}
	}





	[Rpc.Host]
	private void EndFireSale()
	{
		foreach ( var x in FireSaleBoxes )
		{
			if ( x.CanTakeWeapon || x.BoxOpen )
			{
				break;
			}
			x.GameObject?.Destroy();
		}
		if ( FireSaleBoxes.All( n => !n.BoxOpen ) )
		{
			foreach ( var x in FireSaleBoxes )
			{
				x?.Destroy();
			}

			FireSaleStarted = false;
		}

	}

	private void PlayerListChanged( List<Player> playerList )
	{
		Log.Info( "playerlist changed" );
		foreach ( var player in playerList )
		{
			Log.Info( "hello" );
			player.UpdatePlayerList( playerList );
		}
	}




	[Rpc.Host]
	public void AddToPlayerList( Connection connection, Player player )
	{
		var x = PlayersList;
		var y = new PlayerListInfo() { PlayerName = connection.DisplayName, PlayerSteamID = connection.SteamId.ToString(), player = player };
		x.Add( y );
		PlayersList = x;

		//foreach ( var y in PlayerList )
		//{
		//	Log.Info( y.Key.DisplayName );
		//}

		//PlayerListChanged(PlayerList.Values.ToList());
		//Log.Info(connection + " | " +  player);
	}

	[Rpc.Host]
	public void RemoveFromPlayerList( Connection connection )
	{
		/*var list = PlayersList;
		if ( list.Contains( connection ) )
		{
			list.Remove( connection );
			PlayersList = list;
			Log.Info( $"connection {connection.DisplayName} removed" );
		}*/
	}



	[Rpc.Host]
	private void FreezePlayer( Player player )
	{

		player.PlayerController.WalkSpeed = 0;
	}

	[Rpc.Host]
	private void UnFreezePlayers()
	{

		foreach ( var x in PlayerList )
		{

			x.Value.PlayerController.WalkSpeed = 110;

		}
	}

	[Rpc.Broadcast]
	public void TurnPowerOn()
	{
		PowerOn = true;
		Scene.RunEvent<IPower>( x => x.OnPowerTurnedOn() );
		Log.Info( "Power on" );
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;




	}








	public void OnDisconnected( Connection connection )
	{
		Log.Info( $"Disconnected: {connection}" );

		RemoveFromPlayerList( connection );
	}

	public void OnConnected( Connection connection )
	{
		Log.Info( connection.Name + " is connecting" );

		/*if ( connection.SteamId.ToString() == "76561198214368983" || connection.SteamId.ToString() == "76561198125023690" || connection.SteamId.ToString() == "76561198081081329" )
		{
			connection.Kick("fucking dumb idiot stinky pants");
		}*/
	}



	/// <summary>
	/// A client is fully connected to the server. This is called on the host.
	/// </summary>
	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );

		if ( !PlayerPrefab.IsValid() )
			return;

		//
		// Find a spawn location for this player
		//
		var startLocation = FindSpawnLocation().WithScale( 1 );

		// Spawn this object and make the client the owner
		var player = PlayerPrefab.Clone( startLocation, name: $"Player - {channel.DisplayName}" );




		var playerclass = player.GetComponent<Player>();


		AddToPlayerList( channel, playerclass );



		player.NetworkSpawn( channel );




		GivePlayerStartingWeapon( playerclass );




		//if(!GameStarted && !editortesting)
		//{
		//	FreezePlayer(playerclass);
		//}

	}


	/*private void UpdatePlayersHud()
	{
		Log.Info( "private void updatephud" );
		foreach ( var i in PlayerList )
		{
			//Log.Info( i );
			i.Value.UpdatePlayerHud();
		}
	}
*/





	/// <summary>
	/// Find the most appropriate place to respawn
	/// </summary>
	Transform FindSpawnLocation()
	{
		//
		// If they have spawn point set then use those
		//
		if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
		{
			return Random.Shared.FromList( SpawnPoints, default ).WorldTransform;
		}

		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( spawnPoints.Length > 0 )
		{
			return Random.Shared.FromArray( spawnPoints ).WorldTransform;
		}

		//
		// Failing that, spawn where we are
		//
		return WorldTransform;
	}



	private IEnumerable<Zombie> GetAllZombies()
	{
		return Scene.GetAllComponents<Zombie>();
	}


	[Rpc.Host]
	public void DoublePointsStart()
	{
		Log.Info( "double points started" );
		TimeSinceDoublePointsStarted = 0f;
		DoublePointsStarted = true;
		var x = GetAllZombies();
		foreach ( var zombie in x )
		{
			zombie.DoublePointsActivated = true;
		}
	}

	[Rpc.Host]
	public void DoublePointsEnd()
	{
		Log.Info( "double points ended" );
		DoublePointsStarted = false;
		var x = GetAllZombies();
		foreach ( var zombie in x )
		{
			zombie.DoublePointsActivated = false;
		}
	}


	[Rpc.Host]
	public void InstaKillStart()
	{
		Log.Info( "insta kill started" );
		TimeSinceInstaKillStarted = 0f;
		InstaKillStarted = true;
		var x = GetAllZombies();
		foreach ( var zombie in x )
		{
			zombie.Health = 0f;
			zombie.InstaKillActivated = true;
		}
	}





	[Rpc.Host]
	public void InstaKillEnd()
	{
		Log.Info( "insta kill ended" );
		InstaKillStarted = false;
		var x = GetAllZombies();
		foreach ( var zombie in x )
		{
			zombie.Health = CurrentZombiesHealth;
			zombie.InstaKillActivated = true;
		}
	}

	[Rpc.Host]
	public void MaxAmmo()
	{
		foreach ( var x in Scene.GetAllComponents<Player>() )
		{
			Log.Info( "giving a player max ammo" );
			x.Inventory.ServerGiveMaxAmmo();
		}
	}



	[Rpc.Host]
	private void StartRound()
	{
		Round++;
		Log.Info( Round );

		maxZombies = maxZombies + 5 * (int)Math.Ceiling( (double)Round / 2 );
		Log.Info( maxZombies );

		ZombiesSpawned = 0;
		AllZombiesSpawned = false;





	}

	[Rpc.Host]
	public void KillAllZombies()
	{
		var x = Scene.GetAllComponents<Zombie>();
		foreach ( var y in x )
		{
			y.ZombieDead();
		}
	}

	private void WaitToStartGame()
	{
		if ( TimeSinceGameStarted > 7 )
		{
			UnFreezePlayers();
			GameStarted = true;
			StartRound();
		}
	}


	private GameObject RandomZombieSpawn()
	{
		List<GameObject> ValidList = new List<GameObject>();
		foreach ( var x in ZombieSpawnPoints )
		{
			if ( x.activated )
			{
				Log.Info( x );
				ValidList.Add( x.GameObject );

			}
		}

		int randomIndex = rndm.Next( ValidList.Count );
		//Log.Info( ValidList[randomIndex] );
		if ( ValidList[randomIndex] == null ) return null;


		return ValidList[randomIndex];


	}

	[Rpc.Host]
	public void ZombieDeath()
	{
		zombieCount--;
	}

	private void RoundChanged()
	{
		Log.Info( "round changed" );
		ChangeRound?.Invoke();
	}


	private void PlayerListChanged()
	{
		Log.Info( "player list changed" );
		PlayerListChange?.Invoke();
	}


	[Rpc.Host]
	private async void RoundEnd()
	{
		//play round end sound
		//zombieCount = 0;
		//wait 3 seconds
		await Task.Delay( 3000 );



		//start round

		StartRound();

	}

	[Rpc.Host]
	public void SpawnZombie()
	{

		var randomSpawn = RandomZombieSpawn();

		if ( randomSpawn == null )
		{
			Log.Info( "spawn null" );
			return;
		}

		ZombieConfig.Transform.Position = randomSpawn.WorldPosition;

		var asdf = randomSpawn.GetComponent<ZombieSpawn>();

		var zombo = zombiePrefab.Clone( randomSpawn.WorldPosition );
		zombo.GetComponent<SimpleZombieController>().SpawnPoint = asdf;

		zombo.NetworkSpawn();


		zombieCount++;
		ZombiesSpawned++;

		if ( RoundChange )
		{
			RoundChange = false;
		}

		//Zombie.ZombieDied += ZombieDeath;

	}


	private void ZombieSpawning()
	{
		if ( TimeSinceLastZombie >= 3f )
		{
			SpawnZombie();
			TimeSinceLastZombie = 0f;
		}
	}

	private void GetWeaponManager()
	{
		var WeaponManagers = Scene.GetAllComponents<WeaponManager>();

		if ( WeaponManagers == null || WeaponManagers.Count() != 1 )
		{
			Log.Info( "ERROR: Weapon manager null or theres more than 1!" );
		}

		foreach ( var i in WeaponManagers )
		{
			WeaponManager = i;
			Log.Info( "weapon manager found!" );
		}
	}



	public void GivePlayerStartingWeapon( Player player )
	{

		//Log.Info($"giving { Channel.DisplayName } starting weapon");

		//var playerClass = Player.GetComponent<Player>();

		player.SetHealthToMax();
		player.AddPoints( 500 );
		WeaponManager.GivePlayerWeapon( "spaghellim-rottenrounds.prefab", player, 0, 0 );

		//player.ChangeCurrentSlot(1);

	}



	private void HandleZombieSpawning()
	{
		if ( maxZombies > 0 && !AllZombiesSpawned )
		{
			if ( zombieCount < maxZombies )
			{
				ZombieSpawning();

				if ( ZombiesSpawned >= maxZombies )
				{
					AllZombiesSpawned = true;
				}
			}
		}
	}

	private void HandleRoundChanging()
	{
		if ( zombieCount == 0 && !RoundChange )
		{
			RoundChange = true;
			RoundEnd();
		}
	}




	[Rpc.Host]
	private void RestartGame()
	{
		Restarting = true;
		KillAllZombies();
	}



	protected override void OnUpdate()
	{

		if ( IsProxy ) return;

		//Log.Info( ZombieSpawnPoints.Count );

		if ( Networking.IsHost )
		{


			/*if ( editortesting )
			{
				GameStarted = true;
				return;
			}*/

			if ( !GameStarted )
			{
				WaitToStartGame();
			}

			if ( PlayersList.All( (p => p.player.Downed) ) && !Restarting && GameStarted )
			{
				Log.Info( "ALL DEAD! D::::" );
				RestartGame();
			}



			if ( Restarting ) return;


			/*	foreach ( var x in Connection.All )
				{
					if ( x.IsConnecting )
					{
						if ( !connectingConnections.Contains( x ) )
						{
							Log.Info( x.Name + " is connecting" );
							connectingConnections.Add( x );
						}

					}

				}*/


			if ( !GameStarted ) return;


			if ( InstaKillStarted )
			{
				if ( TimeSinceInstaKillStarted >= InstaKillTime )
				{
					InstaKillEnd();
				}
			}

			if ( DoublePointsStarted )
			{
				if ( TimeSinceDoublePointsStarted >= DoublePointsTime )
				{
					DoublePointsEnd();
				}
			}

			if ( FireSaleStarted )
			{
				if ( TimeSinceFireSaleStarted >= FireSaleTime )
				{
					EndFireSale();
				}
			}

			//HandleZombieSpawning();
			//HandleRoundChanging();

		}

	}
}
