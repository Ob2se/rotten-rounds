using Sandbox;
using Sandbox.Citizen;
using Sandbox.UI;
using System;
using System.Net.Http.Headers;
using System.Numerics;

//using rrcustomcontent;
using System.Reflection;
using System.Threading.Tasks;
using static Sandbox.Package;
using static Sandbox.PhysicsContact;



public sealed class Player : Component, IHudInterface
{
	[RequireComponent]
	[Sync]
	public PlayerController PlayerController { get; set; }

	ClothingContainer ClothingContainer { get; set; }

	[Property] SkinnedModelRenderer Body { get; set; }

	[Property] public SkinnedModelRenderer fpsArms { get; set; }

	[Property] GameObject WeaponContainer { get; set; }

	[Property] public GameObject ThirdPersonWeaponModelContainer {get; set;}

	public ModelRenderer ThirdPersonWeaponModel { get; set; }

	[Property] public CameraComponent playerCamera { get; set; }

	CitizenAnimationHelper HandsAnimationHelper;

	public Connection PlayerConnection { get; set; }

	public CitizenAnimationHelper PlayerAnimationHelper { get; set; }
	
	private WeaponManager WeaponManager { get; set; }
	
	public GameObject CurrentWeapon { get; set; }

	[Sync]
	public CitizenAnimationHelper.HoldTypes HoldType { get; set; }

	private float BaseRunSpeed { get; set; } = 200f;
	public int CurrentWeaponType { get; set; }

	GameObject WeaponPrefab { get; set; }
	public int CurrentWeaponSlot { get; set; }

	[Sync] SkinnedModelRenderer WeaponModel { get; set; }

	[Sync] float Health { get; set; }

	[Sync, Change("PointsChanged")] public float Points { get; set; }

	public static event Action PlayerPointsChanged;

	public static event Action UpdateHud;

	[Sync] public bool Downed { get; set; }

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

	[Rpc.Owner]
	private void SetWeaponPosition()
	{
		Log.Info( "set wepaonpos " );
		var boneObject = fpsArms.GetBoneObject( "weapon_IK_hand_R" );
		var bw = CurrentWeapon.GetComponentInChildren<BaseWeapon>();
		
		fpsArms.BoneMergeTarget = bw.WeaponModel;

		Log.Info( fpsArms.BoneMergeTarget );
		
	}


	public void UpdatePlayerHud()
	{
		UpdateHud?.Invoke();
	}


	public void PointsChanged()
	{
		PlayerPointsChanged?.Invoke();
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
	public void AddPoints( int points )
	{
		ChangePointsPos( points );
		Log.Info( Points );
	}

	[Rpc.Broadcast]
	private void ChangePointsPos( int points )
	{
		this.Points += points;
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



	
	private void SpawnWeapon()
	{

		CurrentWeapon?.Destroy();


		var weap = GameObject.GetPrefab( Inventory.Weapons[CurrentWeaponSlot] );
		var weapon = weap.Clone();
		CurrentWeapon = weapon;
		
		CurrentWeaponClass = weapon.GetComponentInChildren<BaseWeapon>();

		CurrentWeaponClass.WeaponModel.RenderType = ModelRenderer.ShadowRenderType.Off;


		CurrentWeapon.SetParent( fpsArms.GameObject, false );

		ClientWeaponEquipped();

		SetWeaponPosition();

		ChangeThirdPersonChar( this );

		int holdtype = ((int)CurrentWeaponClass.WeaponType) + 1;

		ManageHoldType(holdtype);

		Log.Info("current weapon slot: " + CurrentWeaponSlot );
	}


	[Rpc.Owner]
	private void ClientWeaponEquipped()
	{
		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped( CurrentWeapon ) );

	}

	[Rpc.Host]
	private void ServerChangeThidPersonChar()
	{
		Log.Info( Connection.Local.DisplayName + " changing weapon" );
		ChangeThirdPersonChar(this);
	}



	//this is for changing the weapon that appears for other players, purely cosmetic for others, not seen by owner[[[
			
	public void ChangeThirdPersonChar(Player player)
	{
		if ( CurrentWeapon != null )
		{
			Thefuckifiknow(CurrentWeapon.GetComponentInChildren<BaseWeapon>().WeaponModel.Model );
			ManageHoldType(1);
		}
	}

	[Rpc.Host]
	private void Thefuckifiknow(Model currentWeapon)
	{
		
		ThirdPersonWeaponModel?.Destroy();
		ThirdPersonWeaponModel = ThirdPersonWeaponModelContainer.AddComponent<ModelRenderer>();
		
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


	private void GetWeaponManager()
	{
		var WeaponManagers = Scene.GetAllComponents<WeaponManager>();

		if ( WeaponManagers == null || WeaponManagers.Count() != 1 )
		{
			Log.Info( "ERROR: Weapon manager null or theres more than 1!" );
		}

		foreach ( var i in WeaponManagers )
		{
			WeaponManager = i;
			Log.Info( "weapon manager found!" );
		}
	}

	public void UpdatePlayerList()
	{
		PlayerList?.Clear();
		foreach ( var i in Scene.GetAllComponents<Player>() )
		{
			Log.Info( "adding player" );
			AddToPlayerList( i.PlayerConnection, i );
		}

	}


	protected override void OnStart()
	{
		base.OnStart();

		PlayerAnimationHelper = new CitizenAnimationHelper();
		PlayerAnimationHelper.Target = PlayerController.Renderer;

		

		

		PlayerConnection = Connection.Local;

		

		ClothingContainer = ClothingContainer.CreateFromLocalUser();
		ClothingContainer.Apply( PlayerController.Renderer );
		foreach ( var i in Scene.GetAllComponents<Player>() )
		{
			//if ( i == this ) continue;
			AddToPlayerList( i.PlayerConnection, i );
		}


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

			
			//PlayerAnimationHelper.Target = PlayerController.Renderer;

		}

		Log.Info( "player spawned" );

		Scene.RunEvent<IHudInterface>( x => x.UpdatePlayerList() );


		StartingValues();
	}


	
	public void ChangeCurrentSlot(int slot)
	{
		CurrentWeaponSlot = slot;
		SpawnWeapon();
	}


	[Rpc.Owner]
	private void StartingValues()
	{

		if ( Points != 0 || Health != 0 ) return;
		AddPoints( 500 );
		
		Downed = false;
		Health = 100f;
	}


	




	[Rpc.Owner]
	public void SetVMVisibility( bool visibility )
	{
		fpsArms.Enabled = visibility;
		
	}


	private void LineTrace()
	{
		var cameraPos = playerCamera.WorldPosition;
		var direction = playerCamera.WorldRotation.Forward;

		var endPosition = cameraPos + direction * 100;

		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).WithTag( "interactable" ).Run();
		
		if ( traceResult.Hit )
		{
			var inter = traceResult.GameObject.Components.GetAll<IInteraction>().FirstOrDefault();
			inter.OnInteract( this );

		}
	}


	
	public void ServerManageTPV()
	{

		//ManageHoldType();
	}



	public static byte ClampToByte( float value, float min, float max )
	{
		if ( value < min ) value = min;
		if ( value > max ) value = max;
		return (byte)((value - min) / (max - min) * 255f);
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
		if ( isSprinting )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_sprint", true );
		}
		else
		{
			CurrentWeaponClass.WeaponModel.Set( "b_sprint", false );
		}	

	}



	protected override void OnUpdate()
	{
		
		
		if ( IsProxy ) return;
		
		/*if(CurrentWeapon == null)
		{
			ChangeCurrentSlot( 0 );
		}*/	

		if ( Input.Down( "attack2" ) )
		{
			//Log.Info( ThirdPersonWeaponModel.Model.Attachments. );
			/*foreach ( var x in ThirdPersonWeaponModel.Model.Attachments.All )
			{
				Log.Info( x.Name );
			}*/
		}

		if ( isSprinting && (isAiming || isFiring) )
		{
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
			LineTrace();
		}

	}
}
