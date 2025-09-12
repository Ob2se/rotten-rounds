using Sandbox;
using System.Threading.Tasks;

public sealed class WeaponHandler : Component, IWeaponHandler
{

	[Property]
	Player WeaponOwner { get; set; }
	
	BaseWeapon currentWeaponData { get; set; }


	public GameObject ImpactEffectObject { get; set; }

	public GameObject ImpactEffectFlesh { get; set; }
	public GameObject MuzzleFlash { get; set; }

	SkinnedModelRenderer WeaponModel { get; set; }

	TimeSince TimeSinceLastShot;

	TimeSince TimeSinceReloadStarted;

	bool isAutomatic { get; set; }

	float ReloadTime { get; set; }

	int CurrentMag;

	bool Reloading;

	float FireRate;

	int WeaponRange;

	int WeaponDamage;

	int AmmoMax;

	

	int MagMax;

	private void UnADS()
	{
		if ( !this.Network.IsOwner ) return;
		
		currentWeaponData.WeaponModel.Set( "ironsights", 0 );
		WeaponOwner.isAiming = false;
	}


	private void ADS()
	{
		if ( !this.Network.IsOwner ) return;

		if(currentWeaponData == null)
		{
			Log.Info( "but how is this possible" );
		}

		WeaponOwner.isAiming = true;
		currentWeaponData.WeaponModel.Set( "ironsights", 1 );
	}


	
	private void Shoot()
	{
		
		if ( !this.Network.IsOwner ) return;
		
		if ( Reloading ) return;


		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 && WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].AmmoTotal <= 0 ) return;

		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 )
		{
			Reload();
		}
		

		if ( TimeSinceLastShot < 1f / FireRate ) return;

		Log.Info( "aw shhoot" );

		TimeSinceLastShot = 0;

		DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation );

		ServerReduceAmmo( 1 );
		WeaponModel.Set( "b_attack", true );
		WeaponOwner.GetComponentInParent<Player>().fpsArms.Set( "b_attack", true );
		PlayEffects();

	}


	

	[Rpc.Host]
	private void ServerReduceAmmo( int ammo )
	{
		WeaponOwner.Inventory.ReduceCurrentMag( ammo );

	}


	[Rpc.Owner]
	private void DoLineTrace( Vector3 pos, Rotation rot )
	{


		var cameraPos = pos;
		var direction = rot.Forward;

		var endPosition = cameraPos + direction * WeaponRange;
		//DebugOverlay.Line( cameraPos, endPosition, Color.Red, 4f );
		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).IgnoreGameObject( GameObject ).WithoutTags( "winder" ).UseHitboxes().RunAll(); //should probably limit the amount of zombies/objects/thickness of things but fuck it for now
		foreach ( var hit in traceResult )
		{
			if ( hit.Hit )
			{
				handleTrace( hit );
				BulletImpact( hit );
			}
		}

	}


	[Rpc.Broadcast]
	private static void PlaySoundAtLoc( string sound, Vector3 location )
	{
		Sound.Play( sound, location );
	}


	[Rpc.Broadcast]
	private void CreateBulletImpactEffects( Vector3 location, bool flesh, PrefabFile effect)
	{
		if ( !flesh )
		{
			var gHit = GameObject.GetPrefab( effect.ResourcePath ).Clone( location );
			ServerDestroyMS( gHit, 300 );
			

		}
		else
		{
			var gHit = GameObject.GetPrefab( effect.ResourcePath ).Clone( location );
			ServerDestroyMS( gHit, 300 );
		}

	}



	
	private void BulletImpact( SceneTraceResult Hr )
	{
		PlaySoundAtLoc( Hr.Surface.Sounds.Bullet, Hr.EndPosition ); 
		if ( Hr.Tags.Contains( "flesh" ) )
		{
			CreateBulletImpactEffects( Hr.EndPosition, true , currentWeaponData.FleshImpactEffectPrefab );
		}
		else
		{
			CreateBulletImpactEffects( Hr.EndPosition, false, currentWeaponData.ImpactEffectPrefab );
		}

	}



	private void ApplyDamage( Zombie zombie, DamageInfo damageInfo, Vector3 hitPos, int hitBone, Vector3 Direction )
	{

		zombie.TakeDamage( damageInfo, hitPos, hitBone, Direction );
	}


	[Rpc.Owner]
	private void handleTrace( SceneTraceResult hitResult )
	{
		if ( hitResult.Tags.Contains( "zombie" ) )
		{
			DamageInfo damageInfo = new DamageInfo();

			damageInfo.Attacker = GameObject;
			damageInfo.Damage = WeaponDamage;
			var zombie = hitResult.GameObject.GetComponentInChildren<Zombie>();
			if ( zombie == null )
			{
				Log.Info( "zombie null" );
				
			}

			ApplyDamage( zombie, damageInfo, hitResult.EndPosition, hitResult.Bone, hitResult.Direction );

		}
	}

	[Rpc.Owner]
	private void UpdateAmmo()
	{
		WeaponOwner.Inventory.ReduceAmmo( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax - WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag );
		WeaponOwner.Inventory.AddAmmoToMag( currentWeaponData.MagMax );
	}

	//change to timesince
	private void Reload()
	{

		if ( !this.Network.IsOwner ) return;
		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag == WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax || WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].AmmoTotal == 0 ) return;
		Reloading = true;
		UpdateAmmo();
		WeaponModel.Set( "b_reload", true );
		TimeSinceReloadStarted = 0;
		
	}



	[Rpc.Owner]
	private void PlayEffects()
	{
		var mFlash = MuzzleFlash.Clone( WeaponModel.GetAttachment( "muzzle" ).Value );
		ClientDestroyMS( mFlash, 300 );
	}



	private async void ClientDestroyMS( GameObject gameobject, int length )
	{
		await Task.Delay( length );
		gameobject?.Destroy();
	}




	private async void ServerDestroyMS( GameObject gameobject, int length )
	{
		await Task.Delay( length );
		gameobject?.Destroy();
	}

	//[Rpc.Owner]
	public void WeaponEquipped( GameObject weapon )
	{
		if ( !Network.IsOwner ) return;
		Log.Info( "hellllloo" );

		WeaponOwner = GameObject.GetComponent<Player>();
		

		var wClass = weapon.GetComponentInChildren<BaseWeapon>();
		currentWeaponData = wClass;

		WeaponModel = wClass.WeaponModel;


		

		ReloadTime = wClass.ReloadTime;

		isAutomatic = wClass.Automatic;

		MagMax = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax;
		CurrentMag = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag;

		FireRate = wClass.FireRate;

		WeaponRange = wClass.WeaponRange;

		WeaponDamage = wClass.WeaponDamage;

		Log.Info( "path: " + wClass.ImpactEffectPrefab.ResourcePath );
		ImpactEffectObject = GameObject.GetPrefab( wClass.ImpactEffectPrefab.ResourcePath );
		Log.Info("object: " + ImpactEffectObject );

		Log.Info("path: " + wClass.FleshImpactEffectPrefab.ResourcePath );

		ImpactEffectFlesh = GameObject.GetPrefab( wClass.FleshImpactEffectPrefab.ResourcePath );
		Log.Info( "object: " + ImpactEffectFlesh );

		Log.Info("path: " + wClass.MuzzleFlashPrefab.ResourcePath );
		MuzzleFlash = GameObject.GetPrefab( wClass.MuzzleFlashPrefab.ResourcePath );
		Log.Info( "object: " + MuzzleFlash );


	}


	


	private void HandleInputs()
	{


		if ( !Reloading ) {
			
			if ( isAutomatic )
			{
				if ( Input.Down( "Attack1" ) )
				{
					WeaponOwner.isFiring = true;
					Shoot();
				}
				else if ( Input.Released( "Attack1" ) )
				{
					WeaponOwner.isFiring = false;
				}

			}
			else
			{
				if ( Input.Pressed( "Attack1" ) )
				{
					WeaponOwner.isFiring = true;
					Shoot();
				
				}
				if(TimeSinceLastShot > .1f)
				{
					WeaponOwner.isFiring = false;
				}
				if ( Input.Released( "Attack1" ) )
				{
					WeaponOwner.isFiring = false;
				}


			}
		}else if( Reloading )
		{
			WeaponOwner.isFiring = false;
		}

		if ( Input.Down( "Reload" ) )
		{
			Reload();
		}

		if ( Input.Down( "Attack2" ) )
		{
			ADS();
		}
		if ( Input.Released( "Attack2" ) )
		{
			UnADS();
		}

	}


	protected override void OnStart()
	{
		base.OnStart();

	}


	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		base.OnUpdate();
		HandleInputs();

		if ( TimeSinceReloadStarted >= ReloadTime && Reloading == true)
		{
			Reloading = false;
		}

	}
}
