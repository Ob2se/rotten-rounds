using Sandbox;

public sealed class Mysteryboxplacement : Component
{

	[Property]
	public bool StartWithThisPlacement { get; set; }

	[Property]
	public PrefabFile Mysterybox { get; set; }

	[Property]
	public GameObject MysteryboxPlacement { get; set; }


	public bool activated = false;

	[Property, Sync ]
	public GameObject myst { get; private set; }

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
		if ( StartWithThisPlacement )
		{
			//activated = true;
			//SpawnMysteryBox();
		}
	}

	[Rpc.Broadcast]
	public void SpawnMysteryBox()
	{
		myst = GameObject.GetPrefab( Mysterybox.ResourcePath );
		myst.Clone();
		myst.WorldTransform = MysteryboxPlacement.WorldTransform;
	}


	protected override void OnUpdate()
	{
		/*if ( myst.WorldTransform != MysteryboxPlacement.WorldTransform )
		{
			Log.Info( "what the fuck" );
			myst.WorldTransform = MysteryboxPlacement.WorldTransform;
		}*/
	}
}
