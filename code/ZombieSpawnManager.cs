using Sandbox;
using System;

public sealed class ZombieSpawnManager : Component
{


	Random rndm = new Random();


	GameObject spawnPoint;

	[Sync][Property] public GameObject zombiePrefab { get; set; }

	IEnumerable<GameObject> zombieSpawnPoints;
	List<GameObject> zombieSpawnPointsList;
	List<(GameObject, bool)> zombieSpawns;

	protected override void OnStart()
	{
		base.OnStart();

		//should probably be put into its own function to be ran by host
		zombieSpawnPoints = Scene.Directory.FindByName( "zombieSpawn" );
		zombieSpawns = new List<(GameObject, bool)>();

		foreach ( GameObject i in zombieSpawnPoints )
		{
			Log.Info(i.ToString());
			if ( i != null )
			{
				zombieSpawns.Add( (i, true) );
			}
		}

		

	}







	[Rpc.Host]
	private void RandomZombieSpawn()
	{
		
		int randomIndex = rndm.Next( zombieSpawns.Count );
		GameObject randomSpawn = zombieSpawns[randomIndex].Item1;
		if ( randomSpawn != null && zombieSpawns[randomIndex].Item2 == true)
		{
			//zombieSpawns[randomIndex] = (zombieSpawns[randomIndex].Item1, false);
			spawnPoint = randomSpawn;
		}
		else
		{
			spawnPoint = null;
		}
		
	}


	
	public void SpawnZombie()
	{
		Log.Info( "Spawning ZOmbie" );
		RandomZombieSpawn();
		zombiePrefab.Clone(spawnPoint.WorldPosition);
		zombiePrefab.NetworkSpawn();
	}

	protected override void OnUpdate()
	{
		
	}
}
