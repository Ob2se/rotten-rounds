using Sandbox;
using Sandbox.Movement;
using Sandbox.Network;
using System;
using System.ComponentModel.Design;
using System.Data;
using System.IO;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Sandbox.VideoWriter;
using static System.Net.WebRequestMethods;

public sealed class GameModeManager : Component, Component.INetworkListener, IZombieHandler
{

	[Sync( SyncFlags.FromHost ), Change( "RoundChanged" )] public int Round { get; set; }

	[Sync, Change( "PlayerListChange" )] public List<PlayerListInfo> PlayersList { get; set; } = new();

	[Sync] int zombieCount { get; set; }

	[Sync] int maxZombies { get; set; }
	[Sync] int zombiesRemainingInRound { get; set; }

	int zombiesLeft;

	bool StartingBoxSpawned;

	private bool PlayersConnecting { get; set; }

	Random rndm = new Random();


	GameObject spawnPoint;

	bool RoundChange = false;

	bool RoundChanging = false;

	//IEnumerable<GameObject> zombieSpawnPoints;
	//List<GameObject> zombieSpawnPointsList;
	//List<(GameObject, bool)> zombieSpawns;

	[Property]
	private bool editortesting { get; set; } = false;

	public static event Action PlayerListChange;

	public static event Action ChangeRound;

	/*[Sync(SyncFlags.FromHost)]
	private List<Connection> connectingConnections { get; set; } = new();*/


	public bool Restarting { get; set; } = false;

	private float CurrentZombiesHealth = 50f;
	private float CurrentZombiesSpeed = 50f;

	//????? 
	[Sync]
	private TimeSince TimeSinceGameStarted { get; } = 0;


	private TimeSince TimeSinceInstaKillStarted = 0;
	private TimeSince TimeSinceFireSaleStarted = 0;
	private TimeSince TimeSinceDoublePointsStarted = 0;

	private float InstaKillTime = 30f;
	private float FireSaleTime = 30f;
	private float DoublePointsTime = 30f;

	public bool InstaKillStarted = false;
	public bool FireSaleStarted = false;
	public bool DoublePointsStarted = false;

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

	public Mysteryboxplacement currentMysteryboxPlacement;

	private bool MaxZombiesAtOnce = false;
	private bool AllZombiesSpawned = false;

	private int ZombiesSpawned = 0;

	[Sync]
	private bool GameStarted { get; set; } = false;

	[Sync]
	public bool PowerOn { get; set; } = false;


	[Change( "PlayerListChanged" )] public Dictionary<Connection, Player> PlayerList { get; set; } = new();





	CloneConfig ZombieConfig = new CloneConfig();

	//timesince last zombie spawn
	TimeSince TimeSinceLastZombie = 3f;


	TimeSince TimeSinceRoundEnd;


	protected override void OnStart()
	{
		PlayerPrefab = GameObject.GetPrefab( "playercharacter.prefab" );
		zombiePrefab = GameObject.GetPrefab( "zombie.prefab" );

		base.OnStart();

		if ( editortesting )
		{
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
		}


		if ( !GameStarted && Networking.IsHost )
		{


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
					Log.Info( "hellur" );
					if ( !StartingBoxSpawned )
					{
						SpawnMysterbox( mplace );
						StartingBoxSpawned = true;
					}
				}
			}

			var mboxcheckcounter = 0;
			foreach ( var mboxcheck in mysteryboxplacments )
			{
				if ( mboxcheck.StartWithThisPlacement )
				{
					mboxcheckcounter++;
				}
			}

			if ( mboxcheckcounter > 1 )
			{
				Log.Error( "MULTIPLE MYSTERY BOX PLACEMENTS SET TO SPAWN AT START! FIRST POSITION FOUND IS CHOSEN... PLEASE FIX THIS!" );
			}


			var z = Scene.GetAllComponents<ZombieSpawn>();
			foreach ( var zspawn in z )
			{
				Log.Info( zspawn + " a spawn!" );
				ZombieSpawnPoints.Add( zspawn );
			}




		}

		GetWeaponManager();
		if ( Networking.IsHost )
		{
			Zombie.ZombieDied += ZombieDeath;
		}
		//GameStarted = true;
	}




	[Rpc.Host]
	private void SpawnMysterbox( Mysteryboxplacement mysbox )
	{
		if ( currentMysteryboxPlacement != null && currentMysteryboxPlacement.activated )
		{
			currentMysteryboxPlacement.activated = false;
		}
		currentMysteryboxPlacement = mysbox;
		var mbox = GameObject.GetPrefab( mysbox.Mysterybox.ResourcePath );
		var mboxSpawn = mbox.Clone();
		mysbox.activated = true;
		mboxSpawn.WorldTransform = mysbox.MysteryboxPlacement.WorldTransform;
		mboxSpawn.GetComponent<MysteryBox>().Interactable = true;
		mboxSpawn.NetworkSpawn();
	}


	[Rpc.Host]
	public void ChoseNewMBSpot()
	{
		List<Mysteryboxplacement> filtered = new List<Mysteryboxplacement>();
		foreach ( var x in mysteryboxplacments )
		{
			if ( x.activated != true )
			{
				filtered.Add( x );
			}
		}

		// Check if there?s anything left after filtering
		if ( filtered.Count > 0 )
		{
			var random = new Random();
			var randomPlace = filtered[random.Next( filtered.Count )];
			SpawnMysterbox( randomPlace );

		}
		else
		{
			Log.Info( "idiot dumb ass ai" );
		}

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


	[Rpc.Broadcast]
	public void ClearPlayerList()
	{
		PlayerList.Clear();
	}



	[Rpc.Broadcast]
	public void AddToPlayerList( string steamid, Player player )
	{
		var x = PlayersList;
		foreach ( var connection in Connection.All )
		{
			if ( connection.SteamId.ToString() == steamid )
			{
				var y = new PlayerListInfo() { PlayerName = connection.DisplayName, PlayerSteamID = connection.SteamId.ToString(), player = player };
				x.Add( y );
				PlayersList = x;
			}
		}
		//foreach ( var y in PlayerList )
		//{
		//	Log.Info( y.Key.DisplayName );
		//}

		//PlayerListChanged(PlayerList.Values.ToList());
		//Log.Info(connection + " | " +  player);
	}

	[Rpc.Broadcast]
	public void RemoveFromPlayerList( string steamid )
	{
		var list = PlayersList;
		foreach ( var playa in list )
		{
			if ( playa.PlayerSteamID == steamid )
			{
				list.Remove( playa );
				PlayersList = list;
				Log.Info( $"player {playa.PlayerName} removed" );
				return;
			}
		}
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

		RemoveFromPlayerList( connection.SteamId.ToString() );
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


		AddToPlayerList( channel.SteamId.ToString(), playerclass );



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
		int baseZombies = 5;
		int linearGrowth = Round * 3;
		int midRoundBonus = Math.Max( 0, Round - 10 ) * 6;
		int endGameBonus = (int)(Math.Pow( Math.Max( 0, Round - 15 ), 2 ) * 4.5f);
		this.maxZombies = baseZombies + linearGrowth + midRoundBonus + endGameBonus;
		float baseHealth = 60f;
		float linearHealthGrowth = Round * 10f;
		float midRoundHealthBonus = Math.Max( 0, Round - 10 ) * 25f;
		float endGameHealthBonus = (float)(Math.Pow( Math.Max( 0, Round - 15 ), 2 ) * 18f);
		CurrentZombiesHealth = baseHealth + linearHealthGrowth + midRoundHealthBonus + endGameHealthBonus;
		float baseSpeed = 70f;
		float linearSpeedGrowth = Round * 1.8f;
		float midRoundSpeedBonus = Math.Max( 0, Round - 10 ) * 2.5f;
		float endGameSpeedBonus = (float)(Math.Pow( Math.Max( 0, Round - 15 ), 2 ) * 1.2f);
		float maxSpeedCap = 190f;
		CurrentZombiesSpeed = Math.Min( baseSpeed + linearSpeedGrowth + midRoundSpeedBonus + endGameSpeedBonus, maxSpeedCap );
		ZombiesSpawned = 0;
		zombieCount = 0;
		zombiesRemainingInRound = this.maxZombies;
		TimeSinceLastZombie = 0f;

		SpawnZombie();

		AllZombiesSpawned = false;
		RoundChanging = false;

		Log.Info( Round );
		Log.Info( this.maxZombies );


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

		if ( ValidList.Count == 0 )
		{
			return null;
		}

		int randomIndex = rndm.Next( ValidList.Count );
		if ( ValidList[randomIndex] == null )
			return null;

		return ValidList[randomIndex];


	}

	[Rpc.Host]
	public void ZombieDeath()
	{
		zombieCount = Math.Max( 0, zombieCount - 1 );
		zombiesRemainingInRound = Math.Max( 0, zombiesRemainingInRound - 1 );
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
	private void RoundEnd()
	{
		//play round end sound
		//zombieCount = 0;
		//wait 3 seconds
		RoundChanging = true;
		TimeSinceRoundEnd = 0;
		//StartRound();

	}

	[Rpc.Host]
	public void SpawnZombie()
	{
		if ( AllZombiesSpawned || ZombiesSpawned >= maxZombies )
		{
			AllZombiesSpawned = true;
			return;
		}

		if ( zombieCount >= 40 )
		{
			return;
		}
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
		var agent = zombo.GetComponent<NavMeshAgent>();
		var zombie = zombo.GetComponent<Zombie>();

		// Speed scaling follows round pacing set in StartRound.
		agent.MaxSpeed = CurrentZombiesSpeed;

		// Health scaling follows round pacing set in StartRound.
		zombie.Health = CurrentZombiesHealth;

		zombo.NetworkSpawn();


		zombieCount++;
		ZombiesSpawned++;
		if ( ZombiesSpawned >= maxZombies )
		{
			AllZombiesSpawned = true;
		}

		if ( RoundChange )
		{
			RoundChange = false;
		}



	}


	private void ZombieSpawning()
	{
		float baseInterval = 3f;     // Round 1 speed
		float intervalDrop = 0.25f;  // Drop every 5 rounds
		float minInterval = 0.3f;    // Never go faster than this
		int intervalSteps = Math.Max( 0, (Round - 1) / 5 );

		float interval = baseInterval - (intervalSteps * intervalDrop);
		interval -= Math.Max( 0, Round - 15 ) * 0.05f; // Endgame acceleration
		interval = Math.Max( interval, minInterval );

		if ( TimeSinceLastZombie >= interval )
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
		WeaponManager.GivePlayerWeapon( "usp-rottenrounds.prefab", player, 0, 0 );

		//player.ChangeCurrentSlot(1);

	}



	private void HandleZombieSpawning()
	{
		if ( RoundChanging || maxZombies <= 0 || AllZombiesSpawned )
		{
			return;
		}

		if ( zombieCount >= 40 )
		{
			return;
		}

		if ( ZombiesSpawned < maxZombies )
		{
			ZombieSpawning();

			if ( ZombiesSpawned >= maxZombies )
			{
				AllZombiesSpawned = true;
			}
		}
	}

	private void HandleRoundChanging()
	{
		//Log.Info( zombieCount );
		if ( RoundChange )
		{
			RoundEnd();
			RoundChange = false;
		}
	}




	[Rpc.Host]
	private void RestartGame()
	{
		Restarting = true;
		KillAllZombies();
		var scene = Scene.Scene;
		//ResourceLibrary.TryGet<SceneFile>( scene.Source.ResourcePath, out var scenetolaunch );
		var options = new SceneLoadOptions();
		var WeaponPrefabList = WeaponManager.WeaponPaths;
		options.SetScene( scene.Source as SceneFile );
		Game.ChangeScene( options );
		var sceneObject = Game.ActiveScene.CreateObject();
		sceneObject.NetworkMode = NetworkMode.Object;

		var modeManager = sceneObject.AddComponent<GameModeManager>();
		var weaponManager = sceneObject.AddComponent<WeaponManager>();
		weaponManager.WeaponPaths = WeaponPrefabList;

		sceneObject.NetworkSpawn();
		/*Round = 0;
		GameStarted = false;
		RespawnPlayers();
		Restarting = false;*/
	}

	[Rpc.Host]
	private void RespawnPlayers()
	{
		ClearPlayerList();
		foreach ( var y in Scene.GetAllComponents<Player>() )
		{
			y.GameObject.Destroy();
		}
		foreach ( var x in Connection.All )
		{
			var startLocation = FindSpawnLocation().WithScale( 1 );

			// Spawn this object and make the client the owner
			var player = PlayerPrefab.Clone( startLocation, name: $"Player - {x.DisplayName}" );
			var playerclass = player.GetComponent<Player>();


			AddToPlayerList( x.SteamId.ToString(), playerclass );
			player.NetworkSpawn( x );

		}
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

			if ( !RoundChanging && AllZombiesSpawned && zombieCount <= 0 && zombiesRemainingInRound <= 0 )
			{
				RoundEnd();
			}

			if ( RoundChanging && TimeSinceRoundEnd >= 5f )
			{
				StartRound();
			}

			HandleZombieSpawning();
			//HandleRoundChanging();

		}

	}


}
