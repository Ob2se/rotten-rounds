using Sandbox;
using System.Threading.Tasks;
using System;
using System.Runtime.Intrinsics.Arm;
using System.Numerics;
using System.Threading.Channels;
using System.Data;

public sealed class GameModeManager : Component, Component.INetworkListener
{

	[Sync, Change("RoundChanged")] public int Round { get; set; }

	[Sync] int zombieCount { get; set; } = 0;

	[Sync] int maxZombies { get; set; } = 0;

	int zombiesLeft;


	Random rndm = new Random();


	GameObject spawnPoint;

	bool RoundChange = false;

	//IEnumerable<GameObject> zombieSpawnPoints;
	//List<GameObject> zombieSpawnPointsList;
	//List<(GameObject, bool)> zombieSpawns;


	public static event Action ChangeRound;


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
	[Property] public List<GameObject> SpawnPoints { get; set; }

	[Property] public List<GameObject> ZombieSpawnPoints { get; set; }


	private bool MaxZombiesAtOnce = false;
	private bool AllZombiesSpawned = false;

	private int ZombiesSpawned = 0;

	[Sync]
	private bool GameStarted { get; set; } = false;

	[Sync]
	public bool PowerOn { get; set; } = false;

	NetList<Player> PlayerList { get; set; } = new();

	CloneConfig ZombieConfig = new CloneConfig();

	TimeSince TimeSinceLastZombie = 3f;

	protected override void OnStart()
	{
		base.OnStart();
		if ( !GameStarted && Networking.IsHost )
		{ 
			GetWeaponManager();
			StartRound();
			GameStarted = true;
			
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

		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() );
		}
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

		GivePlayerStartingWeapon( channel, player );

		player.NetworkSpawn( channel );

		PlayerList.Add( player.GetComponent<Player>() );
		UpdatePlayersHud();


	}


	private void UpdatePlayersHud()
	{
		Log.Info( "private void updatephud" );
		foreach ( var i in PlayerList )
		{
			//Log.Info( i );
			i.UpdatePlayerHud();
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

	
	private GameObject RandomZombieSpawn()
	{
		var validChoices = ZombieSpawnPoints.Where( x => x.GetComponent<ZombieSpawn>().activated ).ToList();
		int randomIndex = rndm.Next( validChoices.Count );
		GameObject randomSpawn =  validChoices[randomIndex];
		if ( randomSpawn != null && randomSpawn.GetComponent<ZombieSpawn>().activated)
		{
			
			return randomSpawn;
		}
		else
		{
			return null;
		}

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
		}
	}

	
	public void GivePlayerStartingWeapon(Connection Channel, GameObject Player)
	{

		Log.Info($"giving { Channel.DisplayName } starting weapon");

		var playerClass = Player.GetComponent<Player>();

		WeaponManager.GivePlayerStartingWeapon( "TestPistol", playerClass );

		//playerClass.CurrentWeaponSlot = 1;

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

		HandleZombieSpawning();
		HandleRoundChanging();
		
	}








}
