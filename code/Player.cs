using Sandbox;
using Sandbox.Citizen;
using System;
using ZEssentialsTest;
using System.Reflection;
using System.Threading.Tasks;
using static Sandbox.Package;



public sealed class Player : Component
{
	[RequireComponent]
	[Sync]
	private PlayerController PlayerController { get; set; }

	ClothingContainer ClothingContainer { get; set; }

	[Sync] SkinnedModelRenderer Body { get; set; }

	[Property] public SkinnedModelRenderer fpsArms { get; set; }

	[Property] GameObject WeaponContainer { get; set; }

	[Sync, Property] SkinnedModelRenderer ThirdPersonWeaponModel {get; set;}

	[Property] public CameraComponent playerCamera { get; set; }

	CitizenAnimationHelper HandsAnimationHelper;

	public Connection PlayerConnection { get; set; }

	[Property] CitizenAnimationHelper PlayerAnimationHelper { get; set; }
	[Sync] public NetList<WeaponData> Weapons { get; set; } = new();
	[Sync] public GameObject CurrentWeapon { get; set; }

	[Sync] public NetList<int> WeaponAmmo { get; set; } = new();

	GameObject WeaponPrefab { get; set; }
	[Sync] public int CurrentWeaponSlot { get; set; }

	[Sync] SkinnedModelRenderer WeaponModel { get; set; }

	[Sync] float Health { get; set; }

	[Sync, Change("PointsChanged")] public float Points { get; set; }

	public static event Action PlayerPointsChanged;

	public static event Action UpdateHud;

	[Sync] public bool Downed { get; set; }




	private async void SetWeaponPosition()
	{
		Log.Info( "set wepaonpos " );
		var boneObject = fpsArms.GetBoneObject( "weapon_IK_hand_R" );
		var bw = CurrentWeapon.GetComponent<BaseWeapon>();
		await Task.Delay( 1 );
		fpsArms.BoneMergeTarget = bw.WeaponModel;
		//bw.WeaponModel.BoneMergeTarget = fpsArms;
	}


	public void UpdatePlayerHud()
	{
		UpdateHud?.Invoke();
	}


	public void PointsChanged()
	{
		PlayerPointsChanged?.Invoke();
	}


	[Rpc.Host]
	public void GivenWeapon(int slot)
	{
		if ( slot >= 0 && slot < WeaponAmmo.Count )
		{
			WeaponAmmo[slot] = Weapons[slot].MaxAmmo;
		}
		else 
		{
			WeaponAmmo.Add( Weapons[slot].MaxAmmo );
		}
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
		Points += points;
	}

	[Rpc.Owner]
	public void RemovePoints( int points )
	{
		CheckPoints( points );
	}

	[Rpc.Host]
	private void CheckPoints( int points )
	{
		var check = Points - points < 0;
		ChangePointsNeg( check, points );

	}


	[Rpc.Broadcast]
	private void ChangePointsNeg( bool check, int points )
	{
		if ( !check )
		{
			Points -= points;

		}
	}


	//called spawn because it is spawning it, but this is "equipping" a weapon
	[Rpc.Host]
	private void SpawnWeapon()
	{
		Log.Info( "is this running?" );
		var weap = GameObject.GetPrefab( Weapons[CurrentWeaponSlot].PrefabPath );
		var weapon = weap.Clone();
		weapon.NetworkSpawn(PlayerConnection);
		CurrentWeapon = weapon;
		CurrentWeapon.SetParent( fpsArms.GameObject, false );
		fpsArms.AnimationGraph = CurrentWeapon.GetComponent<BaseWeapon>().WeaponModel.AnimationGraph;
		SetWeaponPosition();
		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped(CurrentWeapon) );
	}


	//this is for changing the weapon that appears for other players, purely cosmetic for others, not seen by owner[[[
	[Rpc.Broadcast]		
	private void ChangeThirdPersonChar()
	{
		if ( CurrentWeapon != null )
		{
			ThirdPersonWeaponModel.Model = CurrentWeapon.GetComponent<BaseWeapon>().WeaponModel.Model;
		}
	}

	[Rpc.Broadcast]
	private void ManageHoldType()
	{
		if ( CurrentWeapon == null ) return;
		if ( CurrentWeapon.GetComponent<BaseWeapon>() == null ) return;

		switch ( CurrentWeapon.GetComponent<BaseWeapon>().WeaponType )
		{
			case BaseWeapon.weaponType.Pistol:
				PlayerAnimationHelper.HoldType = CitizenAnimationHelper.HoldTypes.Pistol;
				break;
			case BaseWeapon.weaponType.Smg:
				PlayerAnimationHelper.HoldType = CitizenAnimationHelper.HoldTypes.Shotgun;
				break;

		}

	}




	protected override void OnStart()
	{
		base.OnStart();

		PlayerConnection = Network.Owner;


		ClothingContainer = ClothingContainer.CreateFromLocalUser();
		ClothingContainer.Apply( PlayerController.Renderer );



		if ( IsProxy )
		{
			playerCamera.Enabled = false;
			fpsArms.Enabled = false;


		}
		else
		{
			playerCamera.Enabled = true;
			fpsArms.Enabled = true;
		}

		Log.Info( "player spawned" );

		
		ChangeCurrentSlot( 1 );
		StartingValues();
	}



	[Rpc.Host]
	private void ChangeCurrentSlot(int slot)
	{
		CurrentWeaponSlot = slot - 1;
		SpawnWeapon();
	}


	[Rpc.Broadcast]
	private void StartingValues()
	{

		if ( Points != 0 || Health != 0 ) return;
		Points = 500f;
		
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




	protected override void OnUpdate()
	{

		if ( CurrentWeapon != null )
		{
			ManageHoldType();
		}

		if ( Input.Down( "attack2" ) )
		{
			
		}

		if ( Input.Pressed( "Slot1" ) )
		{
			ChangeCurrentSlot( 1 );
		}

		if ( Input.Pressed( "Slot2" ) )
		{
			ChangeCurrentSlot( 2 );
		}

		if ( Input.Pressed( "Slot3" ) )
		{
			ChangeCurrentSlot( 3 );
		}


		if ( Input.Pressed( "use" ) )
		{
			LineTrace();
		}

	}
}
