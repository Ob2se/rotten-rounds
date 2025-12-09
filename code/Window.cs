using Sandbox;
using System;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public sealed class Window : Component, IInteraction
{

	[Sync, Change( "OnWindowOpen" )] public bool isOpen { get; set; } = false;

	[Sync, Change( "OnBoardChanged" )] public int boards { get; set; } = 6;

	[Sync, Property] public bool isBeingAttacked { get; set; } = false;


	[Property] public int maxBoards { get; set; } = 6;

	[Property] public GameObject BoardsContainer { get; set; }


	[Property] Collider WindowRepairCollider { get; set; }


	

	[Property]
	public float HoldTime => 2f;

	[Property]
	Collider windowTrigger;

	SimpleZombieController SimpleZombieController;

	[Property]
	public GameObject WindowDestroyPoint { get; set; }

	[Property]
	public GameObject WindowEnteredPoint { get; set; }

	[Property]
	public GameObject WindowWaitPoint { get; set; }

	[Property]
	public bool activated;


	NetDictionary<GameObject, bool> WindowBoards;

	public Vector3 WindowDestroyPointPos;

	public Vector3 WindowEnteredPointPos;

	public List<SimpleZombieController> ZombiesInLine = new();
	public bool NeedLineChange = false;

	public SimpleZombieController AttackingZombie;

	[Property]
	public NavMeshLink WindowEntryNavLink { get; set; }

	//[Property, Range] float FingerPlacement { get; set; }


	[Property] BaseSoundComponent SoundPoint { get; set; }

	private void OnBoardChanged(int oldValue, int newValue)
	{
		if ( newValue > oldValue )
		{
			Log.Info( this.ToString() + " add board" );
			//do something when a board is added
		}
		if ( oldValue > newValue )
		{
			Log.Info(this.ToString() + " minus board");
			//do something when a board is removed
			PlayDestroyEffects(this.WorldPosition);
		}
		if ( newValue == 0 )
		{
			isOpen = true;
		}
		if ( newValue > 0 && isOpen )
		{
			isOpen = false;
		}
	}


	public void OnInteract( Player player )
	{
		if ( boards < 6 )
		{
			AddBoard();
			player.AddPoints( 20 );
		}
		
		Log.Info( "window repair" );
	}


	public void OnInteractionFailed( Player player )
	{
		// Optional: Handle interaction failure (e.g., show a message to the player)
	}


	public void ManageZombieLine()
	{
		if ( ZombiesInLine.Count == 0 ) return;
		var x = ZombiesInLine.First();
		x.ChangeState(SimpleZombieController.ZomState.AttackWindow);
		ZombiesInLine.Remove(x);
		isBeingAttacked = true;
	}


	private void PlayBoardAnimationFall()
	{
		var board = GetRandomUnfallenBoard( WindowBoards ).GetComponent<SkinnedModelRenderer>();
		board.Set( "b_fall", true );
	}

	private void PlayBoardAnimationRepair()
	{
		var board = GetRandomFallenBoard( WindowBoards ).GetComponent<SkinnedModelRenderer>();
		board.Set( "b_repair", true );
		//board.PlaybackRate = 1;
	}

	private static void PlaySoundAtLocation( Vector3 Location )
	{
		Sound.Play( "sounds/impacts/melee/impact-melee-wood.sound", Location );
	}

	[Rpc.Broadcast]
	private void PlayDestroyEffects(Vector3 Location)
	{
		PlaySoundAtLocation(Location);
		PlayBoardAnimationFall();
	}

	[Rpc.Broadcast]
	private void PlayRepairEffects( Vector3 Location )
	{
		PlaySoundAtLocation( Location );
		PlayBoardAnimationRepair();
	}

	[Rpc.Host]
	private void OnWindowOpen()
	{
		isBeingAttacked = false;
		Log.Info( "window open" );
		if ( isOpen )
		{
			foreach ( var x in windowTrigger.Touching )
			{
				
			
				//Log.Info( x );
				var zombie = x.GameObject.Components.Get<SimpleZombieController>();
				if ( zombie == null )
				{
					Log.Info( "zombie null" );
				}


				if ( zombie != null )
				{
					//zombie.ChangeState( SimpleZombieController.ZomState.TargetPlayer );
				}
			}
		}
	}



	protected override void OnStart()
	{
		base.OnStart();
		

		WindowDestroyPointPos = WindowDestroyPoint.WorldPosition;
		WindowEnteredPointPos = WindowEnteredPoint.WorldPosition;
		WindowBoards = new();
		foreach ( var x in BoardsContainer.Children )
		{
			if ( x == null )
			{
				Log.Info( "wtf" );
			}
			WindowBoards.TryAdd( x, false );
		}

		if ( Networking.IsHost )
		{
			windowTrigger.OnObjectTriggerEnter += OnTriggerEnter;
		}

	}


	[Rpc.Host]
	private void OnTriggerEnter(GameObject obj)
	{
		Log.Info( obj.ToString() + " entered the trigger box" );

		if ( obj.Parent.Tags.Has( "zombie" ) )
		{

			var zombieController = obj.Parent.Components.Get<SimpleZombieController>();


			if ( zombieController != null && !isOpen && isBeingAttacked == false )
			{
				isBeingAttacked = true;
				AttackingZombie = zombieController;
				zombieController.ChangeState( SimpleZombieController.ZomState.AttackWindow );
			}


			if ( zombieController != null && !isOpen && isBeingAttacked == true && AttackingZombie != zombieController )
			{
				zombieController.ChangeState( SimpleZombieController.ZomState.WaitInLine );
				ZombiesInLine.Add( zombieController );
			}




			if ( zombieController != null && isOpen )
			{

				zombieController.ChangeState( SimpleZombieController.ZomState.EnterWindow );
			}
		}
	}


	private void ZombieEnterWindow()
	{
		if ( isOpen )
		{
			
		}
	}



	public GameObject GetRandomUnfallenBoard( NetDictionary<GameObject, bool> boards )
	{
		var available = boards
			.Where( kv => kv.Value == false )
			.Select( kv => kv.Key )
			.ToList();

		if ( available.Count == 0 )
			return null;

		var rand = new Random();
		var selected = available[rand.Next( available.Count )];

		boards[selected] = true;
		return selected;
	}


	public GameObject GetRandomFallenBoard( NetDictionary<GameObject, bool> boards )
	{
		var available = boards
			.Where( kv => kv.Value == true )
			.Select( kv => kv.Key )
			.ToList();

		if ( available.Count == 0 )
			return null;

		var rand = new Random();
		var selected = available[rand.Next( available.Count )];

		boards[selected] = false;
		return selected;
	}


	[Rpc.Host]
	public void RemoveBoard()
	{
		if ( boards > 0 )
		{
			boards--;
			
		}

	}


	[Rpc.Host]
	public void AddBoard()
	{
		if ( boards != 6 )
		{
			boards++;
			PlayRepairEffects( this.WorldPosition );
		}
	}


	protected override void OnUpdate()
	{

		if ( boards < maxBoards )
		{
			WindowRepairCollider.Tags.Add("interactable");
			WindowRepairCollider.Tags.Add( "interactablehold" );
		} else if ( boards >= maxBoards )
		{
			if ( WindowRepairCollider.Tags.Contains( "interactable" ) )
			{
				WindowRepairCollider.Tags.Remove( "interactable" );
			}
			if( WindowRepairCollider.Tags.Contains( "interactablehold" ) )
			{
				WindowRepairCollider.Tags.Remove( "interactablehold" );
			}
		}


		if ( AttackingZombie == null && NeedLineChange && !isOpen )
		{
			if ( ZombiesInLine.Count == 0 )
			{
				NeedLineChange = false;
				return;
			}
			ManageZombieLine();
			NeedLineChange = false;
		}

		if ( AttackingZombie == null || !AttackingZombie.ZombieClass.isAlive )
		{
			isBeingAttacked = false;
		}

		if ( isOpen )
		{
			isBeingAttacked = false;
		}
	}
}
