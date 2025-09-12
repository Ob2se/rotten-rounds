using Sandbox;
using System.Threading.Tasks;
using System;
using System.Runtime.Intrinsics.Arm;
using System.Numerics;
using System.Threading.Channels;
using System.Data;
using Sandbox.Movement;
using System.ComponentModel.Design;
using System.IO;

public sealed class GameModeManager : Component, Component.INetworkListener, IZombieHandler
{

	[Sync( SyncFlags.FromHost ), Change("RoundChanged")] public int Round { get; set; }

	[Sync] int zombieCount { get; set; }

	[Sync] int maxZombies { get; set; }

	int zombiesLeft;

	private bool PlayersConnecting { get; set; }

	Random rndm = new Random();


	GameObject spawnPoint;

	bool RoundChange = false;

	//IEnumerable<GameObject> zombieSpawnPoints;
	//List<GameObject> zombieSpawnPointsList;
	//List<(GameObject, bool)> zombieSpawns;


	public static event Action ChangeRound;

	/*[Sync(SyncFlags.FromHost)]
	private List<Connection> connectingConnections { get; set; } = new();*/

	


	[Sync]
	private TimeSince TimeSinceGameStarted { get; } = 0;


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


	private bool MaxZombiesAtOnce = false;
	private bool AllZombiesSpawned = false;

	private int ZombiesSpawned = 0;

	[Sync]
	private bool GameStarted { get; set; } = false;

	[Sync]
	public bool PowerOn { get; set; } = false;

	
	public Dictionary<Connection, Player> PlayerList { get; set; } = new();




	CloneConfig ZombieConfig = new CloneConfig();

	TimeSince TimeSinceLastZombie = 3f;

	protected override void OnStart()
	{
		PlayerPrefab = GameObject.GetPrefab( "playercharacter.prefab" );
		zombiePrefab = GameObject.GetPrefab( "zombie.prefab" );

		base.OnStart();
		if ( !GameStarted && Networking.IsHost )
		{

			
			var x = Scene.GetAllComponents<PlayerSpawn>();
			foreach ( var i in x )
			{
				SpawnPoints.Add( i.GameObject );
			}

			var z = Scene.GetAllComponents<ZombieSpawn>();
			foreach ( var zspawn in z )
			{
				Log.Info( zspawn + " a spawn!");
				ZombieSpawnPoints.Add( zspawn );
			}

			


		}

		GetWeaponManager();

	}


	[Rpc.Host]
	public void AddToPlayerList(Connection connection, Player player)
	{
		PlayerList.TryAdd(connection, player);
		Log.Info(connection + " | " +  player);
	}


	[Rpc.Host]
	private void FreezePlayer(Player player)
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

	[Rpc.Host]
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





	public void OnConnected(Connection connection)
	{
		Log.Info( connection.Name + " is connecting" );
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


		

		GivePlayerStartingWeapon( channel, playerclass );




		if(!GameStarted)
		{
			FreezePlayer(playerclass);
		}

	}


	private void UpdatePlayersHud()
	{
		Log.Info( "private void updatephud" );
		foreach ( var i in PlayerList )
		{
			//Log.Info( i );
			i.Value.UpdatePlayerHud();
		}
	}



	[Rpc.Host]
	private void GivePlayersStartingWeapons()
	{
		foreach(var x in PlayerList)
		{
			GivePlayerStartingWeapon( x.Key, x.Value );
		}
	}



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


	[Rpc.Host]
	private void StartRound()
	{
		Round++;
		Log.Info( Round );

		maxZombies = maxZombies + 10 * (int)Math.Ceiling( (double)Round / 2 );
		Log.Info( maxZombies );

		ZombiesSpawned = 0;
		AllZombiesSpawned = false;

		


		
	}



	private void WaitToStartGame()
	{
		if(TimeSinceGameStarted > 7)
		{
			UnFreezePlayers();
			GameStarted = true;
			StartRound();
		}
	}


	private GameObject RandomZombieSpawn()
	{
		List<GameObject> ValidList = new List<GameObject>();
		foreach(var x in ZombieSpawnPoints)
		{
			if(x.activated)
			{
				Log.Info( x );
				ValidList.Add( x.GameObject );
				
			}
		}

		int randomIndex = rndm.Next( ValidList.Count );
		Log.Info( ValidList[randomIndex] );
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
		
		if ( randomSpawn == null ) Log.Info( "wtf!" );

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

		Zombie.ZombieDied += ZombieDeath;

	}

	
	private void  ZombieSpawning()
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


	
	public void GivePlayerStartingWeapon(Connection Channel, Player player)
	{

		Log.Info($"giving { Channel.DisplayName } starting weapon");

		//var playerClass = Player.GetComponent<Player>();

		WeaponManager.GivePlayerWeapon( "usptesting.prefab", player, 0 );

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


	protected override void OnUpdate()
	{

		if ( IsProxy ) return;

		//Log.Info( ZombieSpawnPoints.Count );

		if ( Networking.IsHost )
		{

			if(!GameStarted)
			{
				WaitToStartGame();
			}
			



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

			HandleZombieSpawning();
			HandleRoundChanging();

		}
		
	}


}
