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
using static Sandbox.Citizen.CitizenAnimationHelper;
using static Sandbox.VideoWriter;
using static System.Net.WebRequestMethods;

public sealed class GameModeManager : Component, Component.INetworkListener, IZombieHandler
{

	[Sync( SyncFlags.FromHost ), Change( "RoundChanged" )] public int Round { get; set; }

	[Sync, Change( "PlayerListChange" )] public List<PlayerListInfo> PlayersList { get; set; } = new();

	[Sync] int zombieCount { get; set; }

	[Sync] int maxZombies { get; set; }
	[Sync] int zombiesRemainingInRound { get; set; }


	[Sync]
	private bool RoundFreeze { get; set; } = false;

	int zombiesLeft;

	bool StartingBoxSpawned;

	[Sync] public NetDictionary<long, int> KillList { get; set; } = new();

	[Sync] public NetDictionary<long, int> DownedList { get ; set; } = new();

	[Sync] public NetDictionary<long, int> ReviveList { get; set; } = new();

	[Sync] public NetDictionary<long, float> PointsGainedList { get; set; } = new();

	private bool PlayersConnecting { get; set; }

	Random rndm = new Random();


	GameObject spawnPoint;

	bool RoundChange = false;

	bool RoundChanging = false;



	[Property]
	private bool editortesting { get; set; } = false;

	public static event Action PlayerListChange;

	public static event Action ChangeRound;

	public static event Action<string> PowerUpActivated;

	public static event Action<string> PowerUpDeactivated;


	public float InstaKillTimeLeft => InstaKillStarted ? Math.Max(0, InstaKillTime - TimeSinceInstaKillStarted) : -1;

	public float FireSaleTimeLeft => FireSaleStarted ? Math.Max(0, FireSaleTime - TimeSinceFireSaleStarted) : -1;

	public float DoublePointsTimeLeft => DoublePointsStarted ? Math.Max(0, DoublePointsTime - TimeSinceDoublePointsStarted) : -1;



	public bool Restarting { get; set; } = false;

	private float CurrentZombiesHealth = 50f;
	private float CurrentZombiesSpeed = 50f;

	//????? 
	[Sync]
	private TimeSince TimeSinceGameStarted { get; } = 0;

	[Sync( SyncFlags.FromHost )]
	private TimeSince TimeSinceInstaKillStarted { get; set; } = 0;

	[Sync( SyncFlags.FromHost )]
	private TimeSince TimeSinceFireSaleStarted { get; set; } = 0;

	[Sync( SyncFlags.FromHost )]
	private TimeSince TimeSinceDoublePointsStarted { get; set; } = 0;

	private float InstaKillTime = 30f;
	private float FireSaleTime = 30f;
	private float DoublePointsTime = 30f;

	[Sync( SyncFlags.FromHost )] public bool InstaKillStarted { get; set; }
	[Sync( SyncFlags.FromHost )] public bool FireSaleStarted { get; set; }
	[Sync( SyncFlags.FromHost )] public bool DoublePointsStarted { get; set; }

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

	[Property]
	public GameObject SpectatorPrefab { get; set; }

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


	public List<Connection> Spectators { get; set; } = new();
	public bool GameEnding { get; private set; }

	CloneConfig ZombieConfig = new CloneConfig();

	//timesince last zombie spawn
	TimeSince TimeSinceLastZombie = 3f;


	TimeSince TimeSinceRoundEnd;

	TimeSince TimeSinceGameEnded { get; set; }


	protected override void OnStart()
	{
		PlayerPrefab = GameObject.GetPrefab( "playercharacter.prefab" );
		zombiePrefab = GameObject.GetPrefab( "zombie.prefab" );
		SpectatorPrefab = GameObject.GetPrefab( "prefabs/spectator/spectator.prefab" );

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

	[Rpc.Broadcast]
	public void InvokeFiresalestart()
	{
		PowerUpActivated?.Invoke( "firesale" );
	}

	[Rpc.Broadcast]
	public void InvokeFiresaleend()
	{
		PowerUpDeactivated?.Invoke( "firesale" );
	}



	[Rpc.Host]
	public void StartFireSale()
	{
		InvokeFiresalestart();
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
		InvokeFiresaleend();
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
		PlayersList.Clear();
	}


	[Sync] public bool IsOnslaught { get; set; } = false;

	[Rpc.Host]
	public void StartOnslaught(int zombieamount)
	{
		if ( IsOnslaught ) return;
		IsOnslaught = true;
		KillAllZombies();
		// Set up an onslaught scenario
		maxZombies = zombieamount;
		zombiesRemainingInRound = maxZombies;
		ZombiesSpawned = 0;
		AllZombiesSpawned = false;

		// Crank up the difficulty
		CurrentZombiesHealth *= 1.5f;
		CurrentZombiesSpeed = 190f;
		
		TimeSinceLastZombie = 0f;

		TeleportPlayers();


	}



	[Rpc.Broadcast]
	public void AddToPlayerList( string steamid, Player playerr, float points = -1f )
	{
		var list = PlayersList;

		// Check if the player is already in the list
		var index = list.FindIndex( p => p.PlayerSteamID == steamid );

		if ( index != -1 )
		{
			// Update the existing player instance
			var existing = list[index];
			existing.player = playerr;
			if ( points >= 0f )
				existing.Points = points;
			else if ( playerr != null )
				existing.Points = playerr.Points;
			list[index] = existing;
			PlayersList = list;
			return;
		}

		// Otherwise, find their connection to get their display name
		var conn = Connection.All.FirstOrDefault( c => c.SteamId.ToString() == steamid );
		if ( conn != null )
		{
			var info = new PlayerListInfo()
			{
				PlayerName = conn.DisplayName,
				PlayerSteamID = steamid,
				Points = points >= 0f ? points : (playerr?.Points ?? 0f),
				player = playerr
			};
			list.Add( info );
			PlayersList = list;
		}
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

		foreach ( var x in PlayersList )
		{

			x.player.PlayerController.WalkSpeed = 110;

		}
	}

	[Rpc.Broadcast]
	public void TurnPowerOn()
	{
		PowerOn = true;
		Scene.RunEvent<IPower>( x => x.OnPowerTurnedOn() );
		
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;




	}








	public void OnDisconnected( Connection connection )
	{
		Log.Info( $"Disconnected: {connection}" );
		var playa = PlayersList.FirstOrDefault( p => p.PlayerSteamID == connection.SteamId.ToString() );
		RemoveFromPlayerList( connection.SteamId.ToString() );
		if( Spectators.Contains( connection ) )
		{
			Spectators.Remove( connection );
		}


		var specs = playa.player?.GameObject.GetComponentsInChildren<Spectator>();

		//if disconnected player has a spectator, readd that spectator to the spectators, really should just have the spectators change players but
		if ( specs.Count() > 0 )
		{
			foreach ( var spec in specs )
			{
				var conn = spec.Network.Owner;
				spec.GameObject.Destroy();
				if ( Spectators.Contains( conn ) )
				{
					Spectators.Remove( conn );
				}
				SetJoinedSpectator( conn );
			}
		}



	}

	public void OnConnected( Connection connection )
	{
		Log.Info( connection.Name + " is connecting" );

	}



	/// <summary>
	/// A client is fully connected to the server. This is called on the host.
	/// </summary>
	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );

		//if game is started add to spectators
		if ( GameStarted )
		{
			SetJoinedSpectator( channel );
			return;
		}





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



	private IEnumerable<Zombie> GetAllZombies()
	{
		return Scene.GetAllComponents<Zombie>();
	}


	[Rpc.Broadcast]
	public void InvokeDoublepointsstart()
	{
		PowerUpActivated?.Invoke( "doublepoints" );
	}

	[Rpc.Broadcast]
	public void InvokeDoublepointsend()
	{
		PowerUpDeactivated?.Invoke( "doublepoints" );
	}


	[Rpc.Host]
	public void DoublePointsStart()
	{
		InvokeDoublepointsstart();
		TimeSinceDoublePointsStarted = 0f;
		DoublePointsStarted = true;

	}

	[Rpc.Host]
	public void DoublePointsEnd()
	{
		InvokeDoublepointsend();
		DoublePointsStarted = false;

	}


	[Rpc.Host]
	public void InstaKillStart()
	{
		InvokeInstakillstart();
		TimeSinceInstaKillStarted = 0f;
		InstaKillStarted = true;
		var x = GetAllZombies();
		foreach ( var zombie in x )
		{
			zombie.Health = 1f;
			zombie.InstaKillActivated = true;
		}
	}



	[Rpc.Broadcast]
	public void InvokeInstakillstart()
	{
		PowerUpActivated?.Invoke( "instakill" );
	}

	[Rpc.Broadcast]
	public void InvokeInstakillend()
	{
		PowerUpDeactivated?.Invoke( "instakill" );
	}

	[Rpc.Host]
	public void InstaKillEnd()
	{
		InvokeInstakillend();
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
			x.Inventory.ServerGiveMaxAmmo();
		}
	}


	[Rpc.Host]
	public void SetPlayerSpectator( Player player )
	{
		if ( !Networking.IsHost ) return;
		Log.Info(player.Network.Owner.DisplayName + " is now a spectator");
		
		var conn = player.Network.Owner;
		var steamid = conn.SteamId.ToString();
		
		// Save points before destroying the player
		var savedPoints = player.Points;
		
		var spec = SpectatorPrefab.Clone();
		spec.NetworkSpawn( conn );
		Spectators.Add( conn );
		
		player.GameObject.Destroy();
		
		// Update their playerlist info to signify they are spectating (null player), keep their points
		AddToPlayerList( steamid, null, savedPoints );
	}

	[Rpc.Host]
	public void SetJoinedSpectator( Connection connection )
	{
		if ( !Networking.IsHost ) return;
		var spec = SpectatorPrefab.Clone();
		spec.NetworkSpawn( connection );
		Spectators.Add( connection );
		
		// Register them in the playerlist with a null player object so they show up on scoreboards
		AddToPlayerList( connection.SteamId.ToString(), null, 0f );
	}


	[Rpc.Host]
	public void RespawnPlayer( Connection connection )
	{
		if ( !Networking.IsHost ) return;
		if (Spectators.Contains( connection ) )
		{
			// Find saved points from PlayerListInfo before respawning
			var steamid = connection.SteamId.ToString();
			var existingInfo = PlayersList.FirstOrDefault( p => p.PlayerSteamID == steamid );
			var savedPoints = existingInfo.Points;
		
			var startLocation = FindSpawnLocation().WithScale( 1 );

			// Spawn this object and make the client the owner
			var player = PlayerPrefab.Clone( startLocation, name: $"Player - {connection.DisplayName}" );

			player.NetworkSpawn( connection );

			var playerComp = player.GetComponent<Player>();

			if ( savedPoints <= 0f )
			{
				// Late joiner who never played — give full starting loadout
				GivePlayerStartingWeapon( playerComp );
			}
			else
			{
				// Returning player — restore their saved points
				GivePlayerRespawnWeapon( playerComp );
				playerComp.SetHealthToMax();
				playerComp.AddPoints( (int)savedPoints );
			}

			AddToPlayerList( steamid, playerComp );

		}
	}


	[Rpc.Host]
	public void RespawnAllPlayers()
	{
		if ( !Networking.IsHost ) return;
		foreach ( var connection in Spectators )
		{
			RespawnPlayer( connection );
		}
		Spectators.Clear();
		foreach ( var x in Scene.GetAllComponents<Spectator>() )
		{
			x.GameObject.Destroy();
		}
	}



	[Rpc.Host]
	private void StartRound()
	{
		
		//incr round
		Round++;


		//change server name to current round
		Networking.ServerName = $"Rotten Rounds | In-Game | Round: {Round}";


		//round progression
		int baseZombies = 5;
		int linearGrowth = Round * 3;
		int midRoundBonus = Math.Max( 0, Round - 10 ) * 6;
		int endGameBonus = (int)(Math.Pow( Math.Max( 0, Round - 15 ), 2 ) * 4.5f);
		this.maxZombies = baseZombies + linearGrowth + midRoundBonus + endGameBonus;
		float baseHealth = 40f;
		float linearHealthGrowth = Round * 5f;
		float midRoundHealthBonus = Math.Max( 0, Round - 10 ) * 25f;
		float endGameHealthBonus = (float)(Math.Pow( Math.Max( 0, Round - 15 ), 2 ) * 18f);
		CurrentZombiesHealth = baseHealth + linearHealthGrowth + midRoundHealthBonus + endGameHealthBonus;
		float baseSpeed = 70f;
		float linearSpeedGrowth = Round * 3.8f;
		float midRoundSpeedBonus = Math.Max( 0, Round - 10 ) * 2.5f;
		float endGameSpeedBonus = (float)(Math.Pow( Math.Max( 0, Round - 15 ), 2 ) * 1.2f);
		float maxSpeedCap = 190f;
		CurrentZombiesSpeed = Math.Min( baseSpeed + linearSpeedGrowth + midRoundSpeedBonus + endGameSpeedBonus, maxSpeedCap );
		ZombiesSpawned = 0;
		zombieCount = 0;
		zombiesRemainingInRound = this.maxZombies;
		TimeSinceLastZombie = 0f;

		//spawn zombie
		SpawnZombie();

		AllZombiesSpawned = false;
		RoundChanging = false;



	}

	[Rpc.Host]
	public void KillAllZombies()
	{

		//get all zombies in the scene

		var x = Scene.GetAllComponents<Zombie>();
		foreach ( var y in x )
		{
			//play nuke death

			y.NukeZombie();
		}
	}

	private void WaitToStartGame()
	{

		//loop through connections

		foreach ( var x in Connection.All )
		{

			//if any connection is connecting return;
			if ( x.IsConnecting )
			{
				return;
			}
		}

		//unfreeze players
		UnFreezePlayers();

		//start game
		GameStarted = true;
		StartRound();
		
	}


	private GameObject RandomZombieSpawn()
	{
		List<ZombieSpawn> ValidList = new();
		List<ZombieSpawn> zedSpawnInfluenced = new();

		//go through each zombiespawn
		foreach ( var x in ZombieSpawnPoints )
		{

			//if its activated add it to the valid list
			if ( x.activated )
			{
				ValidList.Add( x );

			}
			//if its activated and in a influence zone add it to the influence zone list
			if ( x.activated && x.InInfluenceZone )
			{
				zedSpawnInfluenced.Add( x );
			}
		}

		//if this list is null there will be no valid spawn anyway so return null

		if ( ValidList.Count == 0 )
		{
			return null;
		}

		//prioritize spawning a zombie in a influence zone
		if ( zedSpawnInfluenced.Count > 0 )
		{
			//Log.Info( "influe count: " + zedSpawnInfluenced.Count );
			int rndIndex = rndm.Next( zedSpawnInfluenced.Count );
			if ( zedSpawnInfluenced[rndIndex] == null ) return null;

			return zedSpawnInfluenced[rndIndex].GameObject;
		}

		//if theres influencezone spawn just get a random valid spawn
		int randomIndex = rndm.Next( ValidList.Count );
		if ( ValidList[randomIndex] == null )
			return null;

		return ValidList[randomIndex].GameObject;


	}

    [Rpc.Host]
    public void ZombieDeath()
    {
        zombieCount = Math.Max(0, zombieCount - 1);
        zombiesRemainingInRound = Math.Max(0, zombiesRemainingInRound - 1);
		
    }



	private void RoundChanged()
	{

		ChangeRound?.Invoke();
	}


	private void PlayerListChanged()
	{

		PlayerListChange?.Invoke();
	}


	[Rpc.Host]
	private void EndGame(GEndReason reason)
	{
		TimeSinceGameEnded = 0f;
		GameEnding = true;
		TellAllToSetHR();
		var end = GameObject.GetPrefab( "prefabs/endgamecamera/endgamecam.prefab" );

		foreach ( var x in Scene.GetAllComponents<Player>() )
		{
			x.DisplayEndGameStats();
		}


		foreach (var conn in Connection.All)
		{
			var cam = end.Clone();
			var ui = cam.GetComponentInChildren<EndGameScreen>();
			
			
			var idek = cam.NetworkSpawn( conn );
			ui.EndReason = reason;
			Log.Info( ui + " and  " + reason );
		}
		
	}

	[Rpc.Broadcast]
	private void TellAllToSetHR()
	{
		_ = SetHighestRound();
	}


	private async Task SetHighestRound()
	{
		//get highest round stat
		var stats = Sandbox.Services.Stats.GetLocalPlayerStats( "clickhq.rottenrounds" );
		await stats.Refresh();
		var HighestRound = stats.Get( "highest_round" );


		//if the current round is greater than highestrounds max then set new value.
		if ( Round > HighestRound.Max )
		{
			Sandbox.Services.Stats.SetValue( "highest_round", Round );
			Log.Info( "new highest round: " + Round );
		}
		else
		{
			Log.Info( "highest round not beaten. Current: " + Round + ", Personal Best: " + HighestRound.Value );
		}
	}


	[Rpc.Host]
	private void RoundEnd()
	{
		//play round end sound


		//respawn any spectators
		if ( Spectators.Count > 0 )
		{
			RespawnAllPlayers();

		}



		RoundChanging = true;
		TimeSinceRoundEnd = 0;

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
		zombie.Health = CurrentZombiesHealth;

		if ( Round >= 5 )
		{ 
			if ( OneIn( 10 ) )
			{
				if ( OneIn( 2 ) ) 
				{ 
					agent.MaxSpeed = 190f;
					zombie.Health = CurrentZombiesHealth / 2;
				}
				else
				{
					agent.MaxSpeed = CurrentZombiesSpeed / 2;
					zombie.Health = CurrentZombiesHealth * 2;
				}
			}
		}
		// Health scaling follows round pacing set in StartRound.

		if ( InstaKillStarted )
		{
			zombie.Health = 1;
		}

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


	//helper method to get one in x value
	public static bool OneIn( int x )
	{
		var Rando = new Random();
		if ( x <= 1 )
			return true; // If someone passes 0 or 1, it's guaranteed true

		return Rando.Next( x ) == 0;
	}



	private void ZombieSpawning()
	{
		float baseInterval = 2f;     // Round 1 speed
		float intervalDrop = 0.35f;  // Drop every 5 rounds
		float minInterval = 0.3f;    // Never go faster than this
		int intervalSteps = Math.Max( 0, (Round - 1) / 5 );

		float interval = baseInterval - (intervalSteps * intervalDrop);
		interval -= Math.Max( 0, Round - 15 ) * 0.05f; // Endgame acceleration
		interval = Math.Max( interval, minInterval );
		if ( IsOnslaught )
		{
			interval = 1f;
		}
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


	//set starting values
	public void GivePlayerStartingWeapon( Player player )
	{

		player.SetHealthToMax();
		player.AddPoints( 500 );
		WeaponManager.GivePlayerWeapon( "usp-rottenrounds.prefab", player, 0, 0 );

	}

	public void GivePlayerRespawnWeapon( Player player )
	{
		player.SetHealthToMax();
		WeaponManager.GivePlayerWeapon( "usp-rottenrounds.prefab", player, 0, 0 );
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

	






	[Rpc.Host]
	private void RestartGame()
	{
		Restarting = true;
		KillAllZombies();
		var scene = Scene.Scene;


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

	}



	[Rpc.Host]
	private void TeleportPlayers()
	{
		
		var list = PlayersList;
		var totalPlayers = list.Count;
		for ( int i = 0; i < totalPlayers; i++ )
		{
			var player = list[i];

			if ( player.player != null )
			{
				Log.Info( "what the freak" );
				MovePlayer( i, totalPlayers );

			}
		}
		
	}


	[Rpc.Broadcast]
	private void MovePlayer( int player, int totalPlayers )
	{
		var playertoteleport = PlayersList[player];
		if ( playertoteleport.player != null )
		{
			playertoteleport.player.GameObject.WorldPosition = GetSpacedTeleportPosition( player, totalPlayers );
		}
	}

	private Vector3 GetTeleportDestinationCenter()
	{
		var teleportLoc = Scene.GetAllComponents<OnslaughtTeleportLoc>().FirstOrDefault();
		return teleportLoc != null ? teleportLoc.GameObject.WorldPosition : Vector3.Zero;
	}

	private Vector3 GetSpacedTeleportPosition( int index, int totalPlayers )
	{
		var center = GetTeleportDestinationCenter();
		var baseHeightOffset = Vector3.Up * 4f;

		if ( totalPlayers <= 1 )
		{
			return ResolveGroundedPosition( center + baseHeightOffset, center );
		}

		const float spacingRadius = 48f;
		int playersInRing = 6;
		int ring = 0;
		int ringIndex = index;

		while ( ringIndex >= playersInRing )
		{
			ringIndex -= playersInRing;
			ring++;
			playersInRing += 6;
		}

		float radius = spacingRadius * (ring + 1);
		float angleStep = (MathF.PI * 2f) / playersInRing;
		float angle = angleStep * ringIndex;
		var horizontalOffset = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * radius;

		var desired = center + horizontalOffset + baseHeightOffset;
		return ResolveGroundedPosition( desired, center );
	}

	private Vector3 GetSpacedReturnPosition( int index, int totalPlayers )
	{
		var center = this.GameObject.WorldPosition;
		var baseHeightOffset = Vector3.Up * 4f;

		if ( totalPlayers <= 1 )
		{
			return ResolveGroundedPosition( center + baseHeightOffset, center );
		}

		const float spacingRadius = 48f;
		int playersInRing = 6;
		int ring = 0;
		int ringIndex = index;

		while ( ringIndex >= playersInRing )
		{
			ringIndex -= playersInRing;
			ring++;
			playersInRing += 6;
		}

		float radius = spacingRadius * (ring + 1);
		float angleStep = (MathF.PI * 2f) / playersInRing;
		float angle = angleStep * ringIndex;
		var horizontalOffset = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * radius;

		var desired = center + horizontalOffset + baseHeightOffset;
		return ResolveGroundedPosition( desired, center );
	}

	private Vector3 ResolveGroundedPosition( Vector3 desired, Vector3 center )
	{
		if ( TryGetGroundedPlacement( desired, out var grounded ) )
		{
			return grounded;
		}

		// Search nearby slots if the chosen position has no walkable ground below.
		for ( int ring = 1; ring <= 3; ring++ )
		{
			float radius = 24f * ring;
			for ( int i = 0; i < 8; i++ )
			{
				float angle = (MathF.PI * 2f / 8f) * i;
				var offset = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * radius;
				var candidate = desired + offset;
				if ( TryGetGroundedPlacement( candidate, out grounded ) )
				{
					return grounded;
				}
			}
		}

		if ( TryGetGroundedPlacement( center, out grounded ) )
		{
			return grounded;
		}

		return desired;
	}

	private bool TryGetGroundedPlacement( Vector3 position, out Vector3 groundedPosition )
	{
		var start = position + Vector3.Up * 80f;
		var end = position + Vector3.Down * 400f;
		var trace = Scene.Trace
			.FromTo( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit || trace.GameObject == null || trace.Normal.z < 0.45f )
		{
			groundedPosition = default;
			return false;
		}

		groundedPosition = trace.HitPosition + Vector3.Up * 6f;
		return true;
	}




	protected override void OnUpdate()
	{

		if ( IsProxy ) return;


		if ( Networking.IsHost )
		{


			if ( GameEnding)
			{
				if ( TimeSinceGameEnded >= 5f )
				{
					var options = new SceneLoadOptions();
					options.SetScene( "scenes/lobby.scene" );

					Game.ChangeScene( options );
				}
				return;
			}


			if ( !GameStarted )
			{
				WaitToStartGame();
			}

			if ( Scene.GetAllComponents<Player>().All( (p => p.Downed) ) && !Restarting && GameStarted && !GameEnding )
			{

				KillAllZombies();
				if(IsOnslaught)
				{
					EndGame(GEndReason.OnSlaughtFailed);
				}
				else
					EndGame(GEndReason.AllPlayersDead);
				return;
			}



			if ( Restarting ) return;



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

			// Safety: reconcile counters with actual scene state to prevent desync deadlocks
			if ( AllZombiesSpawned && !RoundChanging )
			{
				var aliveZombies = Scene.GetAllComponents<Zombie>().Count( z => z.isAlive );
				if ( aliveZombies == 0 && (zombieCount > 0 || zombiesRemainingInRound > 0) )
				{
					Log.Warning( $"Zombie counter desync detected! zombieCount={zombieCount}, zombiesRemaining={zombiesRemainingInRound}, actual alive=0. Forcing round end." );
					zombieCount = 0;
					zombiesRemainingInRound = 0;
				}
			}

			if ( !RoundChanging && AllZombiesSpawned && zombieCount <= 0 && zombiesRemainingInRound <= 0 )
			{
				if ( IsOnslaught )
				{
					EndGame(GEndReason.OnSlaughtSurvived);
					Scene.GetAllComponents<GameEnderBase>().FirstOrDefault()?.GameEndSuccess();
				}
				else
				{
					RoundEnd();
				}
			}

			if ( RoundChanging && TimeSinceRoundEnd >= 5f )
			{
				StartRound();
			}

			HandleZombieSpawning();


		}

	}


}
