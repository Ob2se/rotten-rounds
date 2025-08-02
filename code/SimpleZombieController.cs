using Sandbox;
using Sandbox.Citizen;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Sandbox.PhysicsContact;


public sealed class SimpleZombieController : Component
{


	[Property] NavMeshAgent zombieAgent;


	[Sync] public bool attackingWindow { get; set; } = false;


	[Sync] GameObject targetWindow { get; set; }
	[Sync] Window targetWindowClass { get; set; }

	[Sync] PlayerController targetPlayer { get; set; }


	[Property] CitizenAnimationHelper ZombieAnimationHelper { get; set; }

	Vector3 flatDirection;

	[Property] public Zombie ZombieClass { get; set; }

	Vector3 TargetPosition;

	private TimeSince TimeSinceLastHit = 0f;

	[Property] public ZombieSpawn SpawnPoint { get; set; }

	TimeSince StuckCheckLength;

	[Property]
	Collider ZombieCollider { get; set; }

	//player go list
	IEnumerable<PlayerController> players;

	IEnumerable<GameObject> window;

	[Property]
	private GameObject ZombiePrefabRef;

	public bool EnteringWindow;

	
	TimeSince TimeToEnterWindow;


	bool StuckCheck = false;

	[Property]
	public ZomState CurrentState { get; set; }
	public enum ZomState
	{
		None,
		Spawning,
		Idle,
		TargetWindow,
		WaitInLine,
		TargetPlayer,
		AttackPlayer,
		AttackWindow,
		EnterWindow,
		Dead
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			ZombieStart();
		}
	}



	[Rpc.Host]
	private void ZombieStart()
	{
		players = Scene.GetAllComponents<PlayerController>();


		var winder = SpawnPoint.AssociatedWindow;
		targetWindowClass = winder;
		targetWindow = winder.GameObject;

		CurrentState = ZomState.TargetWindow;
	}


	[Rpc.Broadcast]
	private void MoveZombieTo( Vector3 targetLoc )
	{
		TargetPosition = targetLoc;
		zombieAgent.MoveTo( targetLoc );
	}




	private void GetClosestPlayer()
	{
		//create a dict distance, playerGO
		Dictionary<float, PlayerController> indexs = [];

		//for each player in the playerlist get the distance from the zombie to the player then add their distances and corresponding GO to the dict
		foreach ( PlayerController player in players )
		{
			var distance = Scene.Trace.FromTo( GameObject.WorldPosition, player.WorldPosition ).Run().Distance;
			indexs.TryAdd( distance, player );
		}

		//set the target player to the player that has the min distance
		if ( indexs.TryGetValue( indexs.Keys.Min(), out var tPlayer ) )
		{
			targetPlayer = tPlayer;
		}
	}



	private void DestroyWindow()
	{

		GameObject.GetComponent<SkinnedModelRenderer>().Set( "b_attack", true );
		targetWindowClass.RemoveBoard();
		
	}



	[Rpc.Host]
	private void StopDestroyingWindow()
	{

		attackingWindow = false;
		targetWindowClass.isBeingAttacked = false;
		targetWindowClass.AttackingZombie = null;
		
	}


	[Rpc.Host]
	public void ChangeState( ZomState state )
	{
		CurrentState = state;
	}



	[Rpc.Host]
	private void ZombieState()
	{
		switch ( CurrentState )
		{
			case ZomState.Idle:
				attackingWindow = false;
				zombieAgent.Stop();
				break;
			case ZomState.TargetWindow:
				attackingWindow = false;
				MoveZombieTo( targetWindowClass.WindowDestroyPointPos );
				flatDirection = (targetWindow.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
				WorldRotation = Rotation.LookAt(flatDirection);
				/*if ( GameObject.WorldPosition == TargetPosition && !targetWindowClass.isBeingAttacked )
				{

					ChangeState( ZomState.AttackWindow );
				}*/
				/*if ( targetWindowClass.isBeingAttacked )
				{
					ChangeState( ZomState.WaitInLine );
					targetWindowClass.ZombiesInLine.Add( ZombieClass );
				}*/
				/*if ( GameObject.WorldPosition == TargetPosition && targetWindowClass.isOpen )
				{

					ChangeState( ZomState.EnterWindow );
				}*/
				break;
			case ZomState.TargetPlayer:
				GetClosestPlayer();
				flatDirection = (targetPlayer.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
				WorldRotation = Rotation.LookAt( flatDirection );
				MoveZombieTo( targetPlayer.WorldPosition );
				break;
			case ZomState.AttackPlayer:
				attackingWindow = false;
				break;
			case ZomState.AttackWindow:

				attackingWindow = true;
				targetWindowClass.AttackingZombie = this;
				MoveZombieTo( targetWindowClass.WindowDestroyPointPos );

				if ( attackingWindow && TimeSinceLastHit >= 3.5f)
				{
					TimeSinceLastHit = 0f;
					DestroyWindow();
				}
				if ( targetWindowClass.isOpen )
				{
					ChangeState( ZomState.EnterWindow );
				}
				break;
			case ZomState.EnterWindow:
				if ( !targetWindowClass.isOpen ) Log.Info( "wtf" );
				if ( targetWindowClass.isOpen )
				{

					EnterWindow();
			
				}
				break;
			case ZomState.WaitInLine:
				MoveZombieTo( targetWindowClass.WindowWaitPoint.WorldPosition );

				if ( targetWindowClass.isBeingAttacked == false )
				{
					targetWindowClass.ManageZombieLine();
				}


				if ( targetWindowClass.isOpen )
				{
					ChangeState(ZomState.EnterWindow);
				}
				
				break;
			case ZomState.Dead:
				if ( attackingWindow )
				{
					StopDestroyingWindow();
					targetWindowClass.NeedLineChange = true;
				}
				
				zombieAgent.Stop();
				break;
		}
	}





	//needs polishing and more checks eg: what state theyre in, because some states makes sense for their velocity to be so low
	[Rpc.Host]
	private void CheckIfStuck()
	{

		if ( zombieAgent.Velocity.LengthSquared <= 0.2f )
		{

			if ( !StuckCheck )
			{
				StuckCheckLength = 0;
				StuckCheck = true;
			}
			
			if ( StuckCheckLength > 45f )
			{
				ZombieClass.ZombieDead();
				StuckCheckLength = 0;//change to a respawn, do kill for now
			}
		}
		else { StuckCheck = false; }
	}

	[Rpc.Broadcast]
	public void EnterWindow()
	{

		//MoveZombieTo(targetWindowClass.WindowEnteredPointPos);
		if ( !EnteringWindow )
		{
			TimeToEnterWindow = 0;
			EnteringWindow = true;
		}
		
		if ( TimeToEnterWindow >= 1.5f ) 
		{
			zombieAgent.SetAgentPosition( targetWindowClass.WindowEnteredPointPos );
			GameObject.WorldPosition = targetWindowClass.WindowEnteredPointPos;

			zombieAgent.Enabled = false;
			zombieAgent.Enabled = true;

			ChangeState( ZomState.TargetPlayer );
			targetWindowClass.AttackingZombie = null;
		}
		
	}


	protected override void OnUpdate()
	{

		base.OnUpdate();
		




		if(Networking.IsHost)
		{
			if ( !ZombieClass.isAlive )
			{
				ChangeState( ZomState.Dead );
			}
			ZombieState();
			CheckIfStuck();
			//Log.Info(CurrentState);
		}
		
		ZombieAnimationHelper.WithVelocity( zombieAgent.Velocity );
		
	}
}
