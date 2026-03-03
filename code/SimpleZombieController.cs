using Sandbox;
using Sandbox.Citizen;
using Sandbox.Navigation;
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
	private Vector3 LastMoveTarget;
	private bool HasLastMoveTarget;
	private TimeSince TimeSinceLastMoveCommand;

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
	private bool HasEnteredThroughWindow;

	
	TimeSince TimeToEnterWindow;
	TimeSince TimeSinceRetarget;

	TimeSince TimeSinceLastAttack = 0f;

	bool Attacking = false;
	const float MaxAttackVerticalDelta = 60f;
	const float TargetRetargetInterval = 0.5f;
	const float TargetSwitchAdvantage = 120f;


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
		HasEnteredThroughWindow = false;

		if ( SpawnPoint != null )
		{
			if ( SpawnPoint.RequireAWindow )
			{
				var winder = SpawnPoint.AssociatedWindow;
				targetWindowClass = winder;
				targetWindow = winder.GameObject;
			}
			if(!SpawnPoint.RequireAWindow)
			{
				ChangeState( ZomState.TargetPlayer );
				return;
			}
		}

		

		CurrentState = ZomState.TargetWindow;
	}




	const float RepathInterval = 0.5f;
	const float RepathDistanceThreshold = 25f;

	[Rpc.Host]
	private void MoveZombieTo( Vector3? targetLoc )
	{
		if ( !targetLoc.HasValue ) return;

		var target = targetLoc.Value;
		TargetPosition = target;

		var targetMoved = !HasLastMoveTarget || Vector3.DistanceBetween( target, LastMoveTarget ) > RepathDistanceThreshold;
		var pathStale = TimeSinceLastMoveCommand > RepathInterval;

		if ( targetMoved || pathStale )
		{
			SetZombiePathTo( target );
			LastMoveTarget = target;
			HasLastMoveTarget = true;
			TimeSinceLastMoveCommand = 0f;
		}
	}

	[Rpc.Host]
	private void SetZombiePathTo( Vector3 target )
	{
		var meshTarget = GetNavMeshPointNear( target );
		zombieAgent.MoveTo( meshTarget );
	}

	/// <summary>
	/// Projects a world position onto the navmesh, preferring the point on the same floor.
	/// Samples from target and above to avoid snapping to floors below (e.g. player on 2nd floor).
	/// </summary>
	private Vector3 GetNavMeshPointNear( Vector3 target )
	{
		var heights = new[] { 0f, 64f, 128f, 192f, 256f, 384f };
		Vector3? best = null;
		var bestHeightError = float.MaxValue;

		foreach ( var h in heights )
		{
			var sample = target + Vector3.Up * h;
			var onMesh = Scene.NavMesh.GetClosestPoint( sample );
			if ( !onMesh.HasValue ) continue;
			var heightError = MathF.Abs( onMesh.Value.z - target.z );
			if ( heightError < bestHeightError )
			{
				bestHeightError = heightError;
				best = onMesh;
			}
		}

		return best ?? target;
	}



	[Rpc.Host]
	private void GetClosestPlayer()
	{
		players = Scene.GetAllComponents<Player>();
		if ( players == null )
		{
			targetPlayer = null;
			return;
		}

		if ( players.All( p => p.Downed ) )
		{
			targetPlayer = null;
			ChangeState( ZomState.Idle );
			return;
		}

		if ( targetPlayer != null && !targetPlayer.Downed && TimeSinceRetarget < TargetRetargetInterval )
		{
			return;
		}

		Player closestPlayer = null;
		float closestDistance = float.MaxValue;

		foreach ( var player in players )
		{
			if ( player.Downed ) continue;
			var delta = player.WorldPosition - GameObject.WorldPosition;
			var horizontalDistance = delta.WithZ( 0f ).Length;
			var verticalDelta = MathF.Abs( delta.z );

			// Prefer same-floor targets, but still allow cross-floor via stairs.
			var distance = horizontalDistance + (verticalDelta * 1.75f);

			if ( distance < closestDistance )
			{
				closestDistance = distance;
				closestPlayer = player;
			}
		}

		if ( closestPlayer == null )
		{
			targetPlayer = null;
			return;
		}

		if ( targetPlayer != null && !targetPlayer.Downed )
		{
			var currentDelta = targetPlayer.WorldPosition - GameObject.WorldPosition;
			var currentScore = currentDelta.WithZ( 0f ).Length + (MathF.Abs( currentDelta.z ) * 1.75f);

			// Only switch when clearly better to avoid floor-flip oscillation.
			if ( currentScore <= (closestDistance + TargetSwitchAdvantage) )
			{
				return;
			}
		}

		targetPlayer = closestPlayer;
		TimeSinceRetarget = 0f;
	}



	private bool NearPlayer()
	{
		if ( targetPlayer == null ) return false;

		var delta = targetPlayer.WorldPosition - GameObject.WorldPosition;
		var horizontalDistance = delta.WithZ( 0f ).Length;
		var verticalDelta = MathF.Abs( delta.z );
		if ( horizontalDistance <= 34f && verticalDelta <= MaxAttackVerticalDelta )
		{
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
		if ( !tr.Hit || tr.GameObject == null )
			return;

		var player = tr.GameObject.GetComponentInParent<Player>();
		if ( player == null || player.Downed )
			return;

		player.RemoveHealth( 34 );
			
	}

	private async Task ZombieMeleeAttack()
	{
		if ( Attacking || !ZombieClass.isAlive || targetPlayer == null )
		{
			return;
		}

		Attacking = true;
		TimeSinceLastAttack = 0f;
		ZombieAttackAnim();
		
		ZombieClass.PlayAttackSound();
		await Task.DelayRealtime( 250 );
		
		if ( !ZombieClass.isAlive || targetPlayer == null )
		{
			Attacking = false;
			return;
		}

		DoMeleeTrace();
		Attacking = false;
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
		// One-way progression: once inside, never go back to window flow.
		if ( HasEnteredThroughWindow &&
			CurrentState != ZomState.TargetPlayer &&
			CurrentState != ZomState.AttackPlayer &&
			CurrentState != ZomState.Idle &&
			CurrentState != ZomState.Dead )
		{
			ChangeState( ZomState.TargetPlayer );
		}

		switch ( CurrentState )
		{
			case ZomState.Idle:
				attackingWindow = false;
				zombieAgent.Stop();
				GetClosestPlayer();
				if ( targetPlayer != null )
				{
					ChangeState( HasEnteredThroughWindow ? ZomState.TargetPlayer : ZomState.TargetWindow );
				}
				break;
			case ZomState.TargetWindow:
				if ( HasEnteredThroughWindow )
				{
					ChangeState( ZomState.TargetPlayer );
					break;
				}

				if ( targetWindowClass == null || targetWindow == null )
				{
					ChangeState( ZomState.Idle );
					break;
				}

				attackingWindow = false;
				MoveZombieTo( targetWindowClass.WindowDestroyPointPos );
				flatDirection = (targetWindow.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
				WorldRotation = Rotation.LookAt( flatDirection );

				if ( targetWindowClass.isOpen )
				{
					ChangeState( ZomState.EnterWindow );
					break;
				}

				var distToWindowPoint = Vector3.DistanceBetween( WorldPosition, targetWindowClass.WindowDestroyPointPos );
				if ( distToWindowPoint <= WindowReachDistance )
				{
					if ( targetWindowClass.isBeingAttacked && targetWindowClass.AttackingZombie != this )
					{
						ChangeState( ZomState.WaitInLine );
					}
					else
					{
						ChangeState( ZomState.AttackWindow );
					}
				}
				break;
			case ZomState.TargetPlayer:
				GetClosestPlayer();
				if ( targetPlayer == null )
				{
					ChangeState( ZomState.Idle );
					break;
				}

				flatDirection = (targetPlayer.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
				WorldRotation = Rotation.LookAt( flatDirection );

				MoveZombieTo( targetPlayer.WorldPosition );

				if ( NearPlayer() && TimeSinceLastAttack >= 2f)
				{
					_ = ZombieMeleeAttack();
				}
				break;
			case ZomState.AttackPlayer:
				ChangeState( ZomState.TargetPlayer );
				break;
			case ZomState.AttackWindow:
				if ( HasEnteredThroughWindow )
				{
					ChangeState( ZomState.TargetPlayer );
					break;
				}

				if ( targetWindowClass == null )
				{
					ChangeState( ZomState.Idle );
					break;
				}

				if ( targetWindowClass.isOpen )
				{
					ChangeState( ZomState.EnterWindow );
					break;
				}

				if ( targetWindowClass.isBeingAttacked && targetWindowClass.AttackingZombie != this )
				{
					attackingWindow = false;
					ChangeState( ZomState.WaitInLine );
					break;
				}

				attackingWindow = true;
				targetWindowClass.isBeingAttacked = true;
				targetWindowClass.AttackingZombie = this;
				MoveZombieTo( targetWindowClass.WindowDestroyPointPos );

				if ( TimeSinceLastHit >= 2f )
				{
					TimeSinceLastHit = 0f;
					DestroyWindow();
				}
				break;
			case ZomState.EnterWindow:
				if ( HasEnteredThroughWindow )
				{
					ChangeState( ZomState.TargetPlayer );
					break;
				}

				if ( targetWindowClass == null )
				{
					ChangeState( ZomState.Idle );
					break;
				}

				if ( !targetWindowClass.isOpen )
				{
					ChangeState( ZomState.AttackWindow );
					break;
				}

				EnterWindow();
				break;
			case ZomState.WaitInLine:
				if ( HasEnteredThroughWindow )
				{
					ChangeState( ZomState.TargetPlayer );
					break;
				}

				if ( targetWindowClass == null )
				{
					ChangeState( ZomState.Idle );
					break;
				}

				attackingWindow = false;
				var waitPos = targetWindowClass.WindowWaitPoint != null
					? targetWindowClass.WindowWaitPoint.WorldPosition
					: targetWindowClass.WindowDestroyPointPos;
				MoveZombieTo( waitPos );

				if ( targetWindowClass.isOpen )
				{
					ChangeState( ZomState.EnterWindow );
					break;
				}

				if ( !targetWindowClass.isBeingAttacked || targetWindowClass.AttackingZombie == this )
				{
					ChangeState( ZomState.AttackWindow );
				}
				break;
			case ZomState.Dead:
				if ( attackingWindow && targetWindowClass != null )
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

			// If stalled while chasing a player, force a repath attempt periodically.
			if ( CurrentState == ZomState.TargetPlayer && targetPlayer != null && StuckCheckLength > 1.25f )
			{
				SetZombiePathTo( targetPlayer.WorldPosition );
				LastMoveTarget = targetPlayer.WorldPosition;
				HasLastMoveTarget = true;
				TimeSinceLastMoveCommand = 0f;
				StuckCheckLength = 0f;
			}
		}
		else { StuckCheck = false; }
	}

	const float WindowReachDistance = 45f;

	[Rpc.Broadcast]
	public void EnterWindow()
	{
		MoveZombieTo( targetWindowClass.WindowDestroyPointPos );

		var distToWindow = Vector3.DistanceBetween( WorldPosition, targetWindowClass.WindowDestroyPointPos );
		if ( distToWindow > WindowReachDistance )
		{
			EnteringWindow = false;
			return;
		}

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
			HasEnteredThroughWindow = true;
			HasLastMoveTarget = false;

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
