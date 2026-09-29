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

	public GameObject GO => this.GameObject;


	private bool BeingRepaired = false;

	public bool CanBeRepaired = false;

	private List<Player> PlayersInInterBox = new();

	[Property]
	public bool Hold { get; set; }

	public bool Interactable { get; set; } = false;

	[Property]
	public float HoldTime => 2f;

	[Property]
	Collider windowTrigger;

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

			//do something when a board is added
		}
		if ( oldValue > newValue )
		{

			//do something when a board is removed
			
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
		if ( CanBeRepairedCheck(player) )
		{
			AddBoard();
			player.AddPoints( 20 );
			if ( boards == maxBoards )
			{
				player.CurrentInteraction = null;
			}
		}
		
		//Log.Info( "window repair" );
	}


	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		// Optional: Handle interaction failure (e.g., show a message to the player)
	}


	public void ManageZombieLine()
	{
		// Remove any zombies that were destroyed or killed while waiting in line
		ZombiesInLine.RemoveAll( z => z == null || !z.IsValid || !z.ZombieClass.isAlive );

		if ( ZombiesInLine.Count == 0 ) return;

		var x = ZombiesInLine[0];
		ZombiesInLine.RemoveAt( 0 );

		if ( x != null && x.IsValid )
		{
			x.ChangeState( SimpleZombieController.ZomState.AttackWindow );
			isBeingAttacked = true;
			AttackingZombie = x;
		}
	}


	[Rpc.Broadcast]
	private void PlayBoardAnimationFall()
	{
		var boarder = GetRandomUnfallenBoard( WindowBoards );
		if ( boarder != null )
		{ 
			var board = boarder.GetComponent<SkinnedModelRenderer>();
			board.Set( "b_fall", true );
			
		}
	}

	private void PlayBoardAnimationRepair()
	{
		var boarder = GetRandomFallenBoard( WindowBoards );
		if ( boarder != null )
		{ 
			var board = boarder.GetComponent<SkinnedModelRenderer>();
			board.Set( "b_repair", true );
			//board.PlaybackRate = 1;
			
		}
	}


	[Rpc.Broadcast]
	private static void PlaySoundAtLocation( Vector3 Location )
	{
		Sound.Play( "sounds/impacts/melee/impact-melee-wood.sound", Location );
	}

	
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
		if ( isOpen )
		{
			foreach ( var x in windowTrigger.Touching )
			{
				
			
				//Log.Info( x );
				var zombie = x.GameObject.Components.Get<SimpleZombieController>();
				if ( zombie == null )
				{
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

		WindowRepairCollider.OnObjectTriggerEnter += OnRepairTriggerEnter;
		WindowRepairCollider.OnObjectTriggerExit += OnRepairTriggerExit;
		

		foreach ( var x in BoardsContainer.Children )
		{
			if ( x == null )
			{
				Log.Info( "wtf" );
			}
			WindowBoards.TryAdd( x, false );
		}

		// Sync visual board state for late joiners.
		// The 'boards' count is already synced via [Sync], but the fall animations
		// were sent via Rpc.Broadcast which late joiners never received.
		int boardsDown = maxBoards - boards;
		if ( boardsDown > 0 )
		{
			var allBoards = WindowBoards.Keys.ToList();
			for ( int i = 0; i < boardsDown && i < allBoards.Count; i++ )
			{
				var boardObj = allBoards[i];
				WindowBoards[boardObj] = true;
				var renderer = boardObj.GetComponent<SkinnedModelRenderer>();
				if ( renderer != null )
				{
					renderer.Set( "b_fall", true );
				}
			}
		}

		if ( Networking.IsHost )
		{
			windowTrigger.OnObjectTriggerEnter += OnTriggerEnter;
		}

	}



	private bool CanBeRepairedCheck(Player player)
	{
		if(BeingRepaired) return false;
		if ( boards >= maxBoards ) return false;
		return true;

	}


	private void OnRepairTriggerEnter( GameObject obj )
	{
		//Log.Info( obj.ToString() + " entered the trigger box" );
		var player = obj.GetComponentInParent<Player>();
		if ( player != null )
		{
			if ( !PlayersInInterBox.Contains( player ) )
			{
				PlayersInInterBox.Add( player );
			}
			if ( CanBeRepairedCheck( player ) )
			{
				player.CurrentInteraction = this;
			}


		}
	}


	private void OnRepairTriggerExit( GameObject obj )
	{
		//Log.Info( obj.ToString() + " entered the trigger box" );
		var player = obj.GetComponentInParent<Player>();
		if ( player != null )
		{
			if(PlayersInInterBox.Contains( player ))
			{
				PlayersInInterBox.Remove( player );
				player.CurrentInteraction = null;
			}
		}
	}



	[Rpc.Host]
	private void OnTriggerEnter(GameObject obj)
	{
		//Log.Info( obj.ToString() + " entered the trigger box" );

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
			PlayDestroyEffects( this.WorldPosition );
		}

	}


	[Rpc.Host]
	public void AddBoard()
	{
		if ( boards < maxBoards )
		{
			boards++;
			//Log.Info( "board should be added cuh" );
			PlayRepairEffects( this.WorldPosition );
		}
		
	}


	protected override void OnUpdate()
	{
		if ( boards < maxBoards )
		{
			if ( !WindowRepairCollider.Tags.Contains( "interactable" ) )
			{
				WindowRepairCollider.Tags.Add( "interactable" );
			}

			if ( !WindowRepairCollider.Tags.Contains( "interactablehold" ) )
			{
				WindowRepairCollider.Tags.Add( "interactablehold" );
			}
		}
		else
		{
			if ( WindowRepairCollider.Tags.Contains( "interactable" ) )
			{
				WindowRepairCollider.Tags.Remove( "interactable" );
			}

			if ( WindowRepairCollider.Tags.Contains( "interactablehold" ) )
			{
				WindowRepairCollider.Tags.Remove( "interactablehold" );
			}
		}

		if(boards < maxBoards )
		{
			Interactable = true;
		} else
		{
			Interactable = false;
		}

		if ( (AttackingZombie == null || !AttackingZombie.IsValid) && NeedLineChange && !isOpen )
		{
			if ( ZombiesInLine.Count == 0 )
			{
				NeedLineChange = false;
				return;
			}
			ManageZombieLine();
			NeedLineChange = false;
		}

		if ( AttackingZombie == null || !AttackingZombie.IsValid || !AttackingZombie.ZombieClass.isAlive )
		{
			isBeingAttacked = false;
		}

		if ( isOpen )
		{
			isBeingAttacked = false;
		}

		
	}
}
