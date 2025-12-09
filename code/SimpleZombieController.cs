using Sandbox;
using Sandbox.Citizen;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Sandbox.PhysicsContact;


public sealed class SimpleZombieController : Component
{

	[Property] bool testing { get; set; }

	[Property] NavMeshAgent zombieAgent;


	[Sync] public bool attackingWindow { get; set; } = false;


	[Property, Sync] GameObject targetWindow { get; set; }
	[Property, Sync] Window targetWindowClass { get; set; }

	[Sync] Player targetPlayer { get; set; }


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
	IEnumerable<Player> players;

	IEnumerable<GameObject> window;

	[Property]
	private GameObject ZombiePrefabRef;

	public bool EnteringWindow;

	
	TimeSince TimeToEnterWindow;

	TimeSince TimeSinceLastAttack = 0f;

	bool Attacking = false;


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
		players = Scene.GetAllComponents<Player>();


		var winder = SpawnPoint.AssociatedWindow;
		targetWindowClass = winder;
		targetWindow = winder.GameObject;

		CurrentState = ZomState.TargetWindow;
	}


	[Rpc.Broadcast]
	private void MoveZombieTo( Vector3? targetLoc )
	{
		if(targetLoc.HasValue)
		{
			TargetPosition = targetLoc.Value;
			//Log.Info( targetLoc.Value );
			zombieAgent.MoveTo( targetLoc.Value );
		}
		

	}



	[Rpc.Host]
	private void GetClosestPlayer()
	{
	

		Player closestPlayer = null;
		float closestDistance = float.MaxValue;

		foreach ( var player in players )
		{
			if ( player.Downed ) continue;
			var distance = (GameObject.WorldPosition - player.WorldPosition).Length;

			if ( distance < closestDistance )
			{
				closestDistance = distance;
				closestPlayer = player;
			}
		}

		if ( closestPlayer != null )
		{
			targetPlayer = closestPlayer;
			//Log.Info( closestPlayer );
		}

		if ( players.All( p => p.Downed ) )
		{
			ChangeState( ZomState.Idle );
		}

	}



	private bool NearPlayer()
	{
		//Log.Info( GameObject.WorldPosition.Distance( targetPlayer.WorldPosition ) );
		if(GameObject.WorldPosition.Distance(targetPlayer.WorldPosition) <= 34)
		{
			//Log.Info( "near player" );
			return true;
		}
		return false;
	}


	private void DoMeleeTrace()
	{
		var hand = ZombieClass.zombieModel.GetAttachmentObject( "hold_R" );

		var tr = Scene.Trace.Sphere( 20f, new Vector3(hand.WorldPosition.x, hand.WorldPosition.y + 5, hand.WorldPosition.z ), hand.WorldPosition + hand.WorldRotation.Forward * 60f )
			.WithTag( "player" )
			.Run();

		
		//DebugOverlay.Trace( tr, 2f );
		//Log.Info( tr.GameObject );

		tr.GameObject.GetComponent<Player>().RemoveHealth( 34 );
			
	}

	private async Task ZombieMeleeAttack()
	{
		TimeSinceLastAttack = 0f;
		ZombieAttackAnim();
		
		ZombieClass.PlayAttackSound();
		await Task.DelayRealtime( 250 );
		DoMeleeTrace();
		
	}


	[Rpc.Broadcast]
	private void ZombieAttackAnim()
	{
		ZombieClass.zombieModel.Set( "b_attack", true );
	}
	private void DestroyWindow()
	{

		GameObject.GetComponentInChildren<SkinnedModelRenderer>().Set( "b_attack", true );
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



	public Vector3? GetNearbyAttackPoint( Vector3 origin, float radius = 50f, int maxAttempts = 8 )
	{

		float angle = Game.Random.Float( 0f, 360f );

		// Convert polar coordinates to Cartesian (XZ plane)
		float x = MathF.Cos( angle.DegreeToRadian() ) * radius;
		float y = MathF.Sin( angle.DegreeToRadian() ) * radius;

		// Return a flat position at same height as target
		return origin + new Vector3( x, 0f, y );
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
				
				break;
			case ZomState.TargetPlayer:
				
				
				GetClosestPlayer();
				flatDirection = (targetPlayer.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
				WorldRotation = Rotation.LookAt( flatDirection );
				if ( NearPlayer() && TimeSinceLastAttack >= 2f)
				{
					_ = ZombieMeleeAttack();
				}
				MoveZombieTo( GetNearbyAttackPoint(targetPlayer.WorldPosition, 50f) );
				break;
			case ZomState.AttackPlayer:
				attackingWindow = false;
				break;
			case ZomState.AttackWindow:

				attackingWindow = true;
				targetWindowClass.AttackingZombie = this;
				MoveZombieTo( targetWindowClass.WindowDestroyPointPos );

				if ( attackingWindow && TimeSinceLastHit >= 2f)
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
				if ( !targetWindowClass.isOpen )
				{
					ChangeState( ZomState.AttackWindow );
					break;
				}
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
		if ( testing ) return;
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
			
		}
		
		ZombieAnimationHelper.WithVelocity( zombieAgent.Velocity );
		
	}
}
