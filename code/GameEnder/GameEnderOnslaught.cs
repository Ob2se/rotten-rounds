using Sandbox;
using System;

public sealed class GameEnderOnslaught : GameEnderBase
{

	[Property]
	public List<ZombieSpawn> SpawnsToStart { get; set; } = new List<ZombieSpawn>();

	[Property]
	int ZombieAmount { get; set; } = 10;

	[Property]
	public event Action OnGameEndSuccess;

	[Property]
	public event Action OnGameEnderStart;

	[Rpc.Host]
	public void StopSpawns()
	{
		var zspawns = Scene.GetAllComponents<ZombieSpawn>();
		foreach ( var spawn in zspawns )
		{
			spawn.activated = false;
		}
	}


	[Rpc.Host]
	public void StartSpawns()
	{
		foreach ( var spawn in SpawnsToStart )
		{
			spawn.activated = true;
		}
	}

	public override void GameEndSuccess()
	{
		base.GameEndSuccess();

		OnGameEndSuccess?.Invoke();
		Log.Info( "this would be cash money0" );
	}


	[Rpc.Host]
	public override void StartGameEnder()
	{
		if ( !Interactable ) return;

		OnGameEnderStart?.Invoke();

		StopSpawns();
		StartSpawns();

		var gameModeManager = Scene.GetAllComponents<GameModeManager>().FirstOrDefault();
		if ( gameModeManager != null && !gameModeManager.IsOnslaught )
		{
			gameModeManager.StartOnslaught(ZombieAmount);
			Interactable = false;
		}

		

	}



	protected override void OnUpdate()
	{

	}
}
