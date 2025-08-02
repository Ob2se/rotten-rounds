using Sandbox;
using ZEssentialsTest;

public sealed class WeaponHandler : Component, IWeaponHandler
{

	[Property]
	Player WeaponOwner { get; set; }
	
	BaseWeapon currentWeaponData { get; set; }


	private GameObject ImpactEffectObject { get; set; }

	private GameObject ImpactEffectFlesh { get; set; }
	private GameObject MuzzleFlash { get; set; }

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
	}


	private void ADS()
	{
		if ( !this.Network.IsOwner ) return;
		currentWeaponData.WeaponModel.Set( "ironsights", 1 );
	}


	[Rpc.Owner]
	private void Shoot()
	{
		
		if ( !this.Network.IsOwner ) return;
		
		if ( Reloading ) return;
		
		if ( CurrentMag <= 0 )
		{
			Reload();
		}
		Log.Info( "uhuh " + FireRate );
		if ( TimeSinceLastShot < 1f / FireRate ) return;

		Log.Info( "aw shhoot" );

		TimeSinceLastShot = 0;

		DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation );

		ServerReduceAmmo( 1 );
		WeaponModel.Set( "b_attack", true );
		WeaponOwner.GetComponentInParent<Player>().fpsArms.Set( "b_attack", true );
		PlayEffects();

	}


	[Rpc.Owner]
	private void ClientReduceAmmo( int ammo )
	{
		CurrentMag -= ammo;
	}

	[Rpc.Host]
	private void ServerReduceAmmo( int ammo )
	{
		CurrentMag -= ammo;
		ClientReduceAmmo( 1 );
	}


	[Rpc.Host]
	private void DoLineTrace( Vector3 pos, Rotation rot )
	{


		var cameraPos = pos;
		var direction = rot.Forward;

		var endPosition = cameraPos + direction * WeaponRange;
		//DebugOverlay.Line( cameraPos, endPosition, Color.Red, 4f );
		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).IgnoreGameObject( GameObject.Parent.Parent.Parent.Parent.Parent ).WithoutTags( "winder" ).RunAll(); //should probably limit the amount of zombies/objects/thickness of things but fuck it for now
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
	private void CreateBulletImpactEffects( Vector3 location, bool flesh )
	{
		if ( !flesh )
		{

			var gHit = ImpactEffectObject.Clone( location );
			ServerDestroyMS( gHit, 300 );

		}
		else
		{
			var gHit = ImpactEffectFlesh.Clone( location );
			ServerDestroyMS( gHit, 300 );
		}

	}



	[Rpc.Host]
	private void BulletImpact( SceneTraceResult Hr )
	{
		PlaySoundAtLoc( Hr.Surface.Sounds.Bullet, Hr.EndPosition ); //bug here
		if ( Hr.Tags.Contains( "flesh" ) )
		{
			CreateBulletImpactEffects( Hr.EndPosition, true );
		}
		else
		{
			CreateBulletImpactEffects( Hr.EndPosition, false ); //ill have to do this differently, for now no decals
		}

	}



	private void ApplyDamage( Zombie zombie, DamageInfo damageInfo )
	{

		zombie.TakeDamage( damageInfo );
	}


	[Rpc.Host]
	private void handleTrace( SceneTraceResult hitResult )
	{
		if ( hitResult.Tags.Contains( "zombie" ) )
		{
			DamageInfo damageInfo = new DamageInfo();

			damageInfo.Attacker = GameObject.Parent.Parent.Parent.Parent.Parent; //very questionable
			damageInfo.Damage = WeaponDamage;
			var zombie = hitResult.GameObject.GetComponentInChildren<Zombie>();
			if ( zombie == null )
			{
				Log.Info( "zombie null" );
			}

			ApplyDamage( zombie, damageInfo );

		}
	}

	[Rpc.Broadcast]
	private void UpdateAmmo()
	{
		var ammo1 = MagMax - CurrentMag;
		WeaponOwner.WeaponAmmo[WeaponOwner.CurrentWeaponSlot] -= MagMax + ammo1;
		CurrentMag = MagMax;
	}

	//change to timesince
	private void Reload()
	{

		if ( !this.Network.IsOwner ) return;
		if ( CurrentMag == MagMax ) return;
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


	public void WeaponEquipped( GameObject weapon )
	{

		Log.Info( "hellllloo" );

		var wClass = weapon.GetComponent<BaseWeapon>();
		currentWeaponData = wClass;

		WeaponModel = wClass.WeaponModel;


		AmmoMax = wClass.AmmoMax;

		//ReloadTime = wClass.ReloadTime;

		//isAutomatic = wClass.Automatic;

		MagMax = wClass.MagMax;

		FireRate = wClass.FireRate;

		WeaponRange = wClass.WeaponRange;

		WeaponDamage = wClass.WeaponDamage;

		ImpactEffectObject = GameObject.GetPrefab( wClass.MuzzleFlashPrefab.ResourcePath );

		ImpactEffectFlesh = GameObject.GetPrefab( wClass.FleshImpactEffectPrefab.ResourcePath );
		MuzzleFlash = GameObject.GetPrefab( wClass.MuzzleFlashPrefab.ResourcePath );
	}


	private void HandleInputs()
	{


		if ( isAutomatic )
		{
			if ( Input.Down( "Attack1" ) )
			{
				Log.Info( "uh" );
				Shoot();
				Log.Info( "ok" );
			}
		}
		else
		{
			if ( Input.Pressed( "Attack1" ) )
			{
				Log.Info( "uh" );
				Shoot();
				Log.Info( "ok" );
			}
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
