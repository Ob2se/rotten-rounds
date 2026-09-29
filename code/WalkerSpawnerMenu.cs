using Sandbox;
using System;
public sealed class WalkerSpawnerMenu : Component
{
	
	[Property]
	public GameObject zombiethang { get; set; }


	[Property]
	public GameObject StartLoc { get; set; }

	[Property]
	public GameObject EndLoc { get; set; }

	public GameObject currentZom { get; set; }


	TimeSince TimeSinceStart { get; set; }

	Random rand { get; set; }

	public float RandTime { get; set; }

	bool Start { get; set; }

	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			rand = new Random();
			RandTime = rand.Float( 0, 8 );
			TimeSinceStart = 0;
			Start = true;
		}
			
	}


	public void DeleteZombie()
	{
		if(!IsProxy)
		{
			currentZom?.Destroy();
			SpawnZombie();
		}
		
	}


	public void SpawnZombie()
	{
		var zom = zombiethang.Clone( StartLoc.WorldPosition );
		var walker = zom.GetComponent<ZombieLobbySceneWalker>();
		currentZom = zom;
		if ( walker != null )
		{
			//Log.Info( "maybe this" );
			walker.Spawner = this;
			var rando = new Random();
			walker.Agent.MaxSpeed = rando.Float( 20, 40 );


			walker.MoveIt( EndLoc );

		}
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{

			if ( TimeSinceStart >= RandTime && Start)
			{
				SpawnZombie();
				Start = false;
			}
		}
	}
}
