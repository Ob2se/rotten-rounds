using Sandbox;
using System.Net.Http.Headers;

public sealed class ZombieSpawnController : Component
{

	[Property]
	public List<ZombieSpawn> AssociatedZSpawns { get; set; } = new();

	//public GameModeManager GMManager => Scene.GetAllComponents<GameModeManager>().FirstOrDefault();

	[RequireComponent]
	public HullCollider Trigger { get; set; }


	protected override void OnStart()
	{
		base.OnStart();


		Trigger.OnObjectTriggerEnter += OnPlayerEnter;
		Trigger.OnObjectTriggerExit += OnPlayerExit;
	}


	public void OnPlayerEnter(GameObject obj)
	{
		var player = obj.GetComponent<Player>();
		if ( player == null ) return;
		if ( player.IsValid )
		{
			foreach ( var zSpawn in AssociatedZSpawns )
			{
				if ( !zSpawn.InInfluenceZone )
				{
					
					zSpawn.InInfluenceZone = true;
				}
			}
		}
	}

	public void OnPlayerExit( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if ( player == null ) return;
		foreach ( var touch in Trigger.Touching )
		{
			var touchv = touch.GetComponent<Player>();
			if ( touchv != null)
			{
				return;
			}
			
		}
		foreach ( var zSpawn in AssociatedZSpawns )
		{
			if ( zSpawn.InInfluenceZone )
			{

				zSpawn.InInfluenceZone = false;
			}
		}
	}

	protected override void OnUpdate()
	{

	}
}
