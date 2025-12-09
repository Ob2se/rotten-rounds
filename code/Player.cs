using Sandbox;
using Sandbox.Citizen;
using Sandbox.UI;
using System;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using static Sandbox.Package;
using static Sandbox.PhysicsContact;



public sealed class Player : Component, IHudInterface, IInteraction
{
	[RequireComponent]
	[Sync]
	public PlayerController PlayerController { get; set; }

	public float HoldTime => 0f;

	bool HoldingInteraction { get; set; } = false;
	ClothingContainer ClothingContainer { get; set; }

	[Property] public SkinnedModelRenderer Body { get; set; }

	[Property] public SkinnedModelRenderer fpsArms { get; set; }

	[Property] GameObject WeaponContainer { get; set; }

	[Property, Sync] public GameObject ThirdPersonWeaponModelContainer {get; set;}

	public float BaseHeight { get; } = 72f; 

	public ModelRenderer ThirdPersonWeaponModel { get; set; }

	[Property] public CameraComponent playerCamera { get; set; }

	CitizenAnimationHelper HandsAnimationHelper;

	public Connection PlayerConnection => Connection.Local;

	public CitizenAnimationHelper PlayerAnimationHelper { get; set; }
	
	private WeaponManager WeaponManager { get; set; }
	
	public GameObject CurrentWeapon { get; set; }

	[Sync]
	public CitizenAnimationHelper.HoldTypes HoldType { get; set; }

	[Property]
	private float BaseRunSpeed { get; set; } = 200f;


	[Property]
	private float BaseWalkSpeed { get; set; } = 200f;
	public int CurrentWeaponType { get; set; }

	GameObject WeaponPrefab { get; set; }
	public int CurrentWeaponSlot { get; set; }

	[Sync] SkinnedModelRenderer WeaponModel { get; set; }

	[Sync] public float Health { get; set; }

	[Sync] public float MaxHealth { get; set; } = 100f;

	[Sync, Change("PointsChanged")] public float Points { get; set; }


	public event Action<float> PlayerPointsChanged;

	public event Action PerkListChanged;


	public static event Action AnyPlayerPointsChanged;


	public bool isReviving { get; set; } = false;

	[Sync] public bool Downed { get; set; } = false;

	[Property]
	public InventoryComponent Inventory {  get; set; }

	[Property]
	public PanelComponent Hud { get; set; }

	public bool isAiming { get; set; } = false;

	public bool isSprinting { get; set; } = false;

	public bool isWalking { get; set;} = false;
	public bool isFiring { get; set; } = false;

	public BaseWeapon CurrentWeaponClass { get; set; }


	public string WeaponName => CurrentWeaponClass != null ? CurrentWeaponClass.WeaponName : "None";

	public int AmmoInMag => Inventory.Weapons.Count > 0 ? Inventory.WeaponsAmmo[CurrentWeaponSlot].CurrentMag : 0;
	
	public int AmmoTotal => Inventory.Weapons.Count > 0 ? Inventory.WeaponsAmmo[CurrentWeaponSlot].AmmoTotal : 0;

	public List<Player> PlayerList { get; set; } = new();

	Angles lastEyeAngles;
	float aimPitchInertia;
	float aimYawInertia;

	Rotation lastViewRot;

	public List<string> PerkIcons { get; set; } = new();


	public bool LookingAtInteractable { get; set; } = false;
	public bool LookingAtInteractableRevive { get; set; } = false;
	public bool LookingAtInteractableHold { get; set; } = false;

	public float CurrentInterHoldTime { get; set; } = 0f;
	private IInteraction CurrentInteraction { get; set; }

	[Sync]
	public float ReviveTime { get; set; } = 7f;

	[Sync]
	public bool Reviving { get; set; } = false;

	[Sync]
	private Player ReviveTarget { get; set; }

	public TimeSince TimeSinceInteractHeld { get; set; }

	TimeSince TimeSinceDamagedLast { get; set; }

	TimeSince TimeSinceLastRegen { get; set; }

	[Rpc.Owner]
	private void SetWeaponPosition()
	{
		if ( IsProxy ) return;
		Log.Info( "set wepaonpos " );

		var weap = GameObject.GetPrefab( Inventory.Weapons[CurrentWeaponSlot] );
		var weapon = weap.Clone();
		CurrentWeapon = weapon;
		CurrentWeapon.NetworkMode = NetworkMode.Never;
		CurrentWeaponClass = weapon.GetComponentInChildren<BaseWeapon>();

		CurrentWeaponClass.WeaponModel.RenderType = ModelRenderer.ShadowRenderType.Off;


		CurrentWeapon.SetParent( fpsArms.GameObject, false );

		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped( CurrentWeapon ) );


		var boneObject = fpsArms.GetBoneObject( "weapon_IK_hand_R" );
		var bw = CurrentWeapon.GetComponentInChildren<BaseWeapon>();
		
		fpsArms.BoneMergeTarget = bw.WeaponModel;

		Log.Info( fpsArms.BoneMergeTarget );
		
	}

	public void UpdatePlayerListt( List<Player> newList )
	{
		
	}


	public async Task PerkListIconsChanged(  )
	{
		
		await Task.Delay( 100 );
		Log.Info( "what the nickel" );
		PerkListChanged?.Invoke();
	}

	public void PointsChanged(float oldValue, float newValue)
	{
		var changeAmount = newValue - oldValue;
		Log.Info("hello " + changeAmount);
		PlayerPointsChanged?.Invoke(changeAmount);
		AnyPlayerPointsChanged?.Invoke();
	}


	public void AddToPlayerList( Connection connection, Player player )
	{
		PlayerList.Add( player );
		Log.Info( connection + " | " + player );
	}

	private void DestroyWeapon()
	{
		CurrentWeapon?.Destroy();
	}



	[Rpc.Host]
	public void RemoveHealth(float amount)
	{
		ChangeHealthNeg( amount );
		TakenDamageFeedback();
		if ( this.Health <= 0 )
		{
			PlayerDowned();
		}
		Log.Info( Health );
	}

	[Rpc.Broadcast]
	private void ChangeHealthNeg( float amount )
	{
		this.Health = Math.Clamp( this.Health - amount, 0, this.MaxHealth );
		this.Body.Set( "hit_strength", 1f );
		this.Body.Set( "hit", true );
		TimeSinceDamagedLast = 0f;
	}

	[Rpc.Broadcast]
	private void ChangeHealthPos( float amount )
	{
		this.Health = Math.Clamp( this.Health + amount, 0, this.MaxHealth );
	}

	[Rpc.Host]
	public void AddPoints( int points )
	{
		ChangePointsPos( points );
		
	}

	[Rpc.Broadcast]
	private void ChangePointsPos( int points )
	{
		this.Points += points;
	}

	[Rpc.Broadcast]
	private void ChangeHealthToMax()
	{
		this.Health = this.MaxHealth;
	}


	[Rpc.Host]
	public void RemovePoints( int points )
	{
		CheckPoints( points );
	}

	[Rpc.Host]
	private void CheckPoints( int points )
	{
		var check = this.Points - points < 0;
		ChangePointsNeg( check, points );

	}


	[Rpc.Broadcast]
	private void ChangePointsNeg( bool check, int points )
	{
		if ( !check )
		{
			this.Points -= points;

		}
	}



	[Rpc.Owner]
	private void SpawnWeapon()
	{
		//if ( CurrentWeapon == null ) return;
		
		CurrentWeapon?.Destroy();


		/*var weap = GameObject.GetPrefab( Inventory.Weapons[CurrentWeaponSlot] );
		var weapon = weap.Clone();
		CurrentWeapon = weapon;
		
		CurrentWeaponClass = weapon.GetComponentInChildren<BaseWeapon>();

		CurrentWeaponClass.WeaponModel.RenderType = ModelRenderer.ShadowRenderType.Off;


		CurrentWeapon.SetParent( fpsArms.GameObject, false );

		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped( CurrentWeapon ) );*/


		//ClientWeaponEquipped();

		SetWeaponPosition();

		ChangeThirdPersonChar( this, CurrentWeapon );

		/*int holdtype = ((int)CurrentWeaponClass.WeaponType) + 1;

		ManageHoldType(holdtype);*/

		Log.Info("current weapon slot: " + CurrentWeaponSlot );
	}


	[Rpc.Owner]
	private void ClientWeaponEquipped()
	{
		//fpsArms.AnimationGraph = CurrentWeapon.GetComponentInChildren<BaseWeapon>().WeaponModel.AnimationGraph;
		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped( CurrentWeapon ) );

	}

	[Rpc.Host]
	private void ServerChangeThidPersonChar()
	{
		Log.Info( Connection.Local.DisplayName + " changing weapon" );
		ChangeThirdPersonChar(this, CurrentWeapon);
	}



	//this is for changing the weapon that appears for other players, purely cosmetic for others, not seen by owner[[[
	//[Rpc.Broadcast]
	public void ChangeThirdPersonChar(Player player, GameObject weapon)
	{
		if ( CurrentWeapon != null )
		{
			Thefuckifiknow(CurrentWeapon.GetComponentInChildren<BaseWeapon>().WeaponModel.Model );
			//ManageHoldType(1);
		}
	}

	[Rpc.Broadcast]
	private void Thefuckifiknow(Model currentWeapon)
	{
		
		ThirdPersonWeaponModel?.Destroy();
		if ( ThirdPersonWeaponModelContainer.GetComponent<ModelRenderer>() == null )
		{
			ThirdPersonWeaponModel = ThirdPersonWeaponModelContainer.AddComponent<ModelRenderer>();
		}
		/*else
		{
			ThirdPersonWeaponModel = ThirdPersonWeaponModelContainer.GetComponent<SkinnedModelRenderer>();
		}*/


		ThirdPersonWeaponModel.CreateAttachments = true;
		//ThirdPersonWeaponModel.UseAnimGraph = false;

		ThirdPersonWeaponModel.Model = currentWeapon;
		ThirdPersonWeaponModelContainer.NetworkSpawn();

		/*foreach(var x in ThirdPersonWeaponModel.Model.Attachments.All )
		{
			Log.Info( x.Name );
		}*/
		
	}

	[Rpc.Broadcast]
	public void ManageHoldType(int x)
	{

		Body.Set( "holdtype", x );

	}


	[Rpc.Broadcast]
	public void UpdatePlayerList(List<Player> newList)
	{
		PlayerList?.Clear();
		PlayerList = newList;
		Log.Info( "but why" );
		Log.Info( PlayerList );
	}


	

	protected override void OnStart()
	{
		base.OnStart();

		PlayerAnimationHelper = new CitizenAnimationHelper();
		PlayerAnimationHelper.Target = PlayerController.Renderer;



		

		

		

		/*ClothingContainer = ClothingContainer.CreateFromLocalUser();
		ClothingContainer.Apply( PlayerController.Renderer );*/
		/*foreach ( var i in Scene.GetAllComponents<Player>() )
		{
			//if ( i == this ) continue;
			AddToPlayerList( i.PlayerConnection, i );
		}*/


		if ( IsProxy )
		{
			playerCamera.Enabled = false;
			fpsArms.Enabled = false;
			//PlayerAnimationHelper.Target = PlayerController.Renderer;


		}
		else
		{
			
			playerCamera.Enabled = true;
			fpsArms.Enabled = true;
			Hud.Enabled = true;
			Log.Info( PlayerList.Count );
			SetHealthToMax();
			
			//PlayerAnimationHelper.Target = PlayerController.Renderer;

		}

		Log.Info( "player spawned" );

		//Scene.RunEvent<IHudInterface>( x => x.UpdatePlayerList() );


		StartingValues();
	}


	[Rpc.Host]
	public void SetHealthToMax()
	{
		ChangeHealthToMax();
	}


	public void ChangeCurrentSlot(int slot)
	{
		CurrentWeaponSlot = slot;
		SpawnWeapon();
	}


	[Rpc.Host]
	private void StartingValues()
	{

		if ( Points != 0 && Health != 0 ) return;
		/*SetHealthToMax();
		AddPoints( 500 );*/
		
	}


	




	[Rpc.Owner]
	public void SetVMVisibility( bool visibility )
	{
		fpsArms.Enabled = visibility;
		
	}


	private SceneTraceResult? LineTrace()
	{
		var cameraPos = playerCamera.WorldPosition;
		var direction = playerCamera.WorldRotation.Forward;

		var endPosition = cameraPos + direction * 100;

		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).WithTag( "interactable" ).Run();

		if ( traceResult.Hit )
		{
			return traceResult;
		}
		else
		{
			return null;
		}
	}
	public void OnInteractionFailed( Player player )
	{
		//could add some feedback here later
	}

	public void OnInteract( Player player )
	{
		if ( !Downed ) return;
		Log.Info( "oninteract2" );
		//RevivePlayer();
		
	}
	


	[Rpc.Host]
	public void RevivePlayer()
	{
		if ( !Downed ) {  return; }
		SetPlayerUps();
		
	}

	[Rpc.Broadcast]
	private void SetPlayerUps()
	{
		
		this.Downed = false;
		this.Body.Set( "special_movement_states", 4 );
		this.PlayerController.IsDucking = false;
		this.PlayerController.WalkSpeed = BaseWalkSpeed;
		this.PlayerController.RunSpeed = BaseRunSpeed;
		this.PlayerController.BodyHeight = BaseHeight;
		this.GameObject.Tags.Remove( "interactable" );
		this.GameObject.Tags.Remove( "downed" );
	}


	public static byte ClampToByte( float value, float min, float max )
	{
		if ( value < min ) value = min;
		if ( value > max ) value = max;
		return (byte)((value - min) / (max - min) * 255f);
	}


	[Rpc.Owner]
	private void TakenDamageFeedback()
	{
		Sound.Play( "sound/hits/hitsounds.sound", GameObject.WorldPosition);
		playerCamera.WorldRotation = new Rotation( -100, -100, -100, 1);
	}


	[Rpc.Host]
	private void PlayerDowned()
	{
		SetPlayerDowns();
	}

	[Rpc.Broadcast]
	private void SetPlayerDowns()
	{
		
		this.Downed = true;
		Body.Set( "special_movement_states", 3 );
		PlayerController.WalkSpeed = 25f;
		PlayerController.DuckedSpeed = 25f;
		PlayerController.IsDucking = true;
		this.PlayerController.BodyHeight = 30;
		GameObject.Tags.Add( "interactable" );
		GameObject.Tags.Add( "downed" );
	}

	public void UpdateWeaponSway( )
	{

		
		Rotation viewRot = Scene.Camera.WorldRotation.Angles();

		
		Rotation deltaRot = viewRot * lastViewRot.Inverse;

		
		Angles deltaAngles = deltaRot.Angles();

		
		aimPitchInertia = aimPitchInertia.LerpTo( deltaAngles.pitch * 3f, Time.Delta * 50f );
		aimYawInertia = aimYawInertia.LerpTo( deltaAngles.yaw * 3f, Time.Delta * 50f );

		CurrentWeaponClass.WeaponModel.Set( "aim_pitch_inertia", aimPitchInertia );
		CurrentWeaponClass.WeaponModel.Set( "aim_yaw_inertia", aimYawInertia );

		lastViewRot = viewRot;
	}




	private void ControlMovingAnimationFPS()
	{
		var speed = PlayerController.Velocity.WithZ( 0 ).Length;
		float normalizedSpeed = ClampToByte( speed, 0f, 200f );

		//float moveBobFloat = moveBobFloat.LerpTo( target, Time.Delta * 5f );

		CurrentWeaponClass.WeaponModel.Set( "move_bob", normalizedSpeed );
		if ( isReviving )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_grab", true );
		}
		if ( !isReviving )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_grab", false);
		}

		if ( isSprinting )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_sprint", true );
		}
		if ( !isSprinting )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_sprint", false );
		}

	}


	[Rpc.Host]
	private void RegenHealth()
	{

		ChangeHealthPos( .5f );
		//Log.Info( Connection.Local.DisplayName + Health );
			
		
	}


	[Rpc.Host]
	public void DownedAnimControlHost()
	{
		DownedAnimControl();
	}

	[Rpc.Broadcast]
	private void DownedAnimControl()
	{
		
		Body.Set( "move_direction",250 );
		Body.Set( "move_groundspeed", 1000 );
		Body.Set( "move_speed", 1000 );
	}



	private void InteractionTags(SceneTraceResult traceResult)
	{
		var inter = traceResult.GameObject.Components.GetAll<IInteraction>().FirstOrDefault();
		if ( traceResult.GameObject.Tags.Has( "downed" ) )
		{
			inter.OnInteract( traceResult.GameObject.GetComponent<Player>() );
			return;
		}


		inter.OnInteract( this );
	}

	[Rpc.Host]
	private void RevivingPlayer(Player player)
	{
		player.RevivePlayer();
	}


	private void ConstInterTrace()
	{
		var useTrace = LineTrace();
		if ( useTrace == null )
		{
			LookingAtInteractable = false;
			LookingAtInteractableRevive = false;
			LookingAtInteractableHold = false;
			/*HoldingInteraction = false;
			if(Input.Down( "use" ) )
			{
				Input.ReleaseAction( "use" );
			}*/
			return;
		}
		if(useTrace.HasValue)
		{
			if(useTrace.Value.Tags.Contains( "downed" ) )
			{
				LookingAtInteractableRevive = true;
				return;
			}
			if(useTrace.Value.Tags.Contains( "interactablehold" ) )
			{
				LookingAtInteractableHold = true;
				return;
			}
			LookingAtInteractable = true;
			return;
		}
		return;
	}



	protected override void OnUpdate()
	{
		
		
		if ( IsProxy ) return;

		ConstInterTrace();

		if(CurrentWeapon != null)
		{
			int holdtype = ((int)CurrentWeaponClass.WeaponType) + 1;

			ManageHoldType( holdtype );
		}

		if ( Input.Down( "attack2" ) && CurrentWeapon != null )
		{
			
		}

		if ( Health < MaxHealth && TimeSinceDamagedLast >= 5f)
		{
			RegenHealth();
		}

		if ( Downed )
		{
			//Log.Info( "downed" );
			SetPlayerDowns();
			DownedAnimControl();
		}
		if ( !Downed && Reviving )
		{
			SetPlayerUps();
		}

		if ( isSprinting && (isAiming || isFiring) )
		{
			//if ( !CurrentWeaponClass.Automatic ) return;
			PlayerController.RunSpeed = PlayerController.WalkSpeed;
			isSprinting = false;
		}
		else
		{
			PlayerController.RunSpeed = BaseRunSpeed;
		}

		if ( CurrentWeapon != null )
		{
			ControlMovingAnimationFPS();
			UpdateWeaponSway( );
		}


		if( PlayerController.Velocity.LengthSquared >= 0.2f )
		{
			isWalking = true;
		}
		else
		{
			isWalking = false;
		}

		if ( Input.Down( "run" ) && PlayerController.Velocity.LengthSquared >= 0.2f)
		{
			isSprinting = true;
		}
		else if ( Input.Released( "run" ) || PlayerController.Velocity.LengthSquared <= 0.2f )
		{
			isSprinting = false;
		}


		if ( Input.Pressed( "Slot1" ) )
		{
			

			if ( CurrentWeaponSlot == 0 ) return;
			ChangeCurrentSlot( 0 );
		}

		if ( Input.Pressed( "Slot2" ) )
		{
			if ( Inventory.Weapons.Count < 2 ) return;

			if ( CurrentWeaponSlot == 1 ) return;
			ChangeCurrentSlot( 1 );
		}

		if ( Input.Pressed( "Slot3" ) )
		{
			if ( Inventory.Weapons.Count < 3 ) return;
			if ( CurrentWeaponSlot == 3 ) return;
			ChangeCurrentSlot( 2 );
		}


		if ( Input.Pressed( "use" ) )
		{
			TimeSinceInteractHeld = 0;
			var useTrace = LineTrace();
			if ( useTrace != null )
			{
				var inter = useTrace.Value.GameObject.Components.GetAll<IInteraction>().FirstOrDefault();
				if ( !useTrace.Value.GameObject.Tags.Has( "downed" ) )
				{
					Log.Info(useTrace.Value.GameObject);
					if(useTrace.Value.GameObject.Tags.Has( "interactablehold" ) )
					{
						Log.Info( "interactable hold" );
						HoldingInteraction = true;
						CurrentInterHoldTime = inter.HoldTime;
						CurrentInteraction = inter;
						return;
					}
					inter.OnInteract( this );
					if ( useTrace.Value.GameObject.Tags.Has( "door" ) )
					{
						CurrentWeaponClass.WeaponModel.Set( "speed_grab", .5f);
						CurrentWeaponClass.WeaponModel.Set( "grab_action", 4 );
					}
					else 
					{
						CurrentWeaponClass.WeaponModel.Set( "speed_grab", .75f );
						CurrentWeaponClass.WeaponModel.Set( "grab_action", 2 );
					}
				}
				if ( useTrace.Value.GameObject.Tags.Has( "downed" ) )
				{
					ReviveTarget = useTrace.Value.GameObject.GetComponent<Player>();
				}
				
			}
			
		}

		if ( Input.Down( "use" ) )
		{
			Log.Info( "holding use" );
			
			
			if ( isWalking )
			{
				Input.ReleaseAction( "use" );
				return;
			}

			TimeSinceInteractHeld += Time.Delta;

			if ( HoldingInteraction )
			{
				if ( TimeSinceInteractHeld >= CurrentInterHoldTime )
				{
					CurrentInteraction.OnInteract( this );
					TimeSinceInteractHeld = 0;
				}
			}


			if ( ReviveTarget != null )
			{
				isReviving = true;
			}
			if(ReviveTarget == null)
			{
				isReviving = false;
			}
			if ( isReviving )
			{
				if ( TimeSinceInteractHeld > ReviveTime )
				{
					RevivingPlayer( ReviveTarget );
				}
			}
		}

		if(Input.Released( "use" ) )
		{
			TimeSinceInteractHeld = 0;
			HoldingInteraction = false;
			isReviving = false;
			ReviveTarget = null;

		}
		if ( !Input.Down( "use" ) )
		{
			TimeSinceInteractHeld = 0;
		}


	}
}
