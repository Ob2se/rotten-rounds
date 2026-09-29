using Sandbox;
using Sandbox.Citizen;
using System;

public sealed class ZombieLobbySceneWalker : Component
{

	[Property]
	public NavMeshAgent Agent { get; set; }

	public WalkerSpawnerMenu Spawner { get; set; }

	[Property]
	public CitizenAnimationHelper ZombieAnimationHelper {  get; set; }

	public Vector3 TargetLoc;

	public void MoveIt(GameObject EndLoc)
	{
		Agent.MoveTo( EndLoc.WorldPosition );
		TargetLoc = EndLoc.WorldPosition;
		//Log.Info( Scene.NavMesh.IsGenerating );
	}

	private bool NearPos()
	{

		var delta = TargetLoc - GameObject.WorldPosition;
		var horizontalDistance = delta.WithZ( 0f ).Length;
		if ( horizontalDistance <= 34f )
		{
			return true;
		}
		return false;
	}


	protected override void OnUpdate()
	{
		ZombieAnimationHelper.WithVelocity( Agent.Velocity );

		if ( NearPos()  )
		{
			//Log.Info( "worketh?" );
			Spawner.DeleteZombie();
		}
	}
}
