using Sandbox;
using System;
using System.Threading.Tasks;
using static Sandbox.Citizen.CitizenAnimationHelper;

public sealed class WeaponHandler : Component, IWeaponHandler
{

	[Property]
	Player WeaponOwner { get; set; }

	BaseWeapon currentWeaponData;


	public GameObject ImpactEffectObject;

	public GameObject ImpactEffectFlesh;
	public GameObject MuzzleFlash;

	SkinnedModelRenderer WeaponModel;

	TimeSince TimeSinceLastShot;

	TimeSince TimeSinceReloadStarted;

	TimeSince TimeSinceShotHeld;
	TimeSince TimeSinceLastBulletReload;


	private Queue<Angles> RecoilQueue = new();

	bool PumpAction;
	bool BoltAction;

	bool isAutomatic;

	float ReloadTime;

	int CurrentMag;

	bool Reloading;
	bool ReloadingSingleBullet;

	float FireRate;

	int WeaponRange;

	int WeaponDamage;

	int AmmoMax;

	int MagMax;

	float WeaponPower;

	[Property]
	public float LauncherProjectileSpeed { get; set; } = 2300f;

	[Property]
	public float LauncherExplosionRadius { get; set; } = 280f;


	float PumpTime;
	float BoltTime;

	BaseWeapon.weaponType WeaponType;



	float RecoilStrength = 2f;


	private float recoilPitch;
	private float RecoilSpeed = 250f;
	private float recoverySpeed = 15f;

	Angles lastEyeAngles;
	float aimPitchInertia;
	float aimYawInertia;

	Angles recoilTarget;
	Angles currentRecoil;

	private Angles recoilOffset; // current visual offset
	private Angles recoilVelocity; // optional, for smoothing
	private Angles currentRecoilLastFrame;
	private float shotInaccuracy;
	private float horizontalRecoilBias;

	private Random Rand = new Random();

	private void UnADS()
	{
		if ( !this.Network.IsOwner ) return;

		if ( WeaponType == BaseWeapon.weaponType.Sniper )
		{
			WeaponOwner.Hud.Enabled = true;
			WeaponOwner.SniperScope.Enabled = false;
			WeaponOwner.fpsArms.RenderOptions.Game = true;
			//WeaponOwner.playerCamera.FieldOfView = Preferences.FieldOfView;
			WeaponModel.RenderOptions.Game = true;
		}


		currentWeaponData.WeaponModel.Set( "ironsights", 0 );
		WeaponOwner.isAiming = false;
	}


	private void ADS()
	{
		if ( !this.Network.IsOwner ) return;

		if ( currentWeaponData == null )
		{
			Log.Info( "but how is this possible" );
		}




		WeaponOwner.isAiming = true;
		currentWeaponData.WeaponModel.Set( "ironsights", 1 );
		currentWeaponData.WeaponModel.Set( "ironsights_fire_scale", .25f );


		if ( WeaponType == BaseWeapon.weaponType.Sniper )
		{
			WeaponOwner.Hud.Enabled = false;
			WeaponOwner.SniperScope.Enabled = true;
			WeaponOwner.fpsArms.RenderOptions.Game = false;
			//WeaponOwner.playerCamera.FieldOfView = 20f;
			WeaponModel.RenderOptions.Game = false;
		}

	}


	[Rpc.Owner]
	private void Shoot()
	{

		if ( !this.Network.IsOwner ) return;



		//if ( Reloading ) return;

		//if(CurrentMag <= 0 ) return;

		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 && WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].AmmoTotal <= 0 ) return;

		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 )
		{
			if ( WeaponType == BaseWeapon.weaponType.Shotgun )
			{
				StartSingleBulletReload();
			}
			if ( WeaponType != BaseWeapon.weaponType.Shotgun )
			{
				StartReload();
			}

		}


		if ( !BoltAction && !PumpAction )
		{
			float secondsPerShot = 60f / FireRate;
			if ( TimeSinceLastShot < secondsPerShot ) return;
		}
		else if ( BoltAction || PumpAction )
		{
			if ( TimeSinceLastShot < PumpTime || TimeSinceLastShot < .75f ) return;
		}


		if ( WeaponType == BaseWeapon.weaponType.Shotgun )
		{
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, true, true );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, true, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, true, false );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, false, false );
		}
		else
		{
			if ( WeaponType == BaseWeapon.weaponType.Launcher )
			{
				
			}
			else
			{
				DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming, true, true );
			}
		}


		AddRecoil( RecoilStrength );
		ServerReduceAmmo( 1 );
		WeaponModel.Set( "b_attack", true );
		WeaponOwner.fpsArms.Set( "b_attack", true );
		PlayEffects();
		ShootTP();

		if ( BoltAction )
		{
			WeaponModel.Set( "b_reload_bolt", true );
			WeaponOwner.fpsArms.Set( "b_reload_bolt", true );
		}


		//TPEffect();
		//var idek = WeaponOwner.ThirdPersonWeaponModel.GetAttachmentObject( "muzzle" ).WorldTransform;
		//ThirdpersonEffects(idek);
		PlayGunShotSound();
		TimeSinceLastShot = 0;
	}

	


	[Rpc.Broadcast]
	private void PlayGunShotSound()
	{
		var x = Sound.Play( "sound/testing/9mmshot-[audiotrimmer.com]4.sound", GameObject.WorldPosition );
		//x.SetParent( currentWeaponData.GameObject );
	}

	[Rpc.Host]
	private void ServerReduceAmmo( int ammo )
	{
		WeaponOwner.Inventory.ReduceCurrentMag( ammo );

	}


	public void AddRecoil( float amount )
	{
		float adsMul = WeaponOwner.isAiming ? 0.72f : 1f;
		float moveSpeed = WeaponOwner.PlayerController.Velocity.WithZ( 0f ).Length;
		float runSpeed = Math.Max( 1f, WeaponOwner.PlayerController.RunSpeed );
		float moveMul = 1f + Math.Clamp( moveSpeed / runSpeed, 0f, 1f ) * 0.35f;
		float typeMul = GetRecoilTypeMultiplier();

		float pitchKick = Rand.Float( 0.9f, 1.35f ) * amount * adsMul * moveMul * typeMul;
		float yawKick = Rand.Float( 0.01f, 0.07f ) * amount * adsMul * moveMul;
		if ( Rand.Int( 0, 100 ) < 65 )
			yawKick *= (horizontalRecoilBias >= 0f) ? 1f : -1f;
		else
			yawKick *= Rand.Float( -1f, 1f );

		horizontalRecoilBias = MathX.Lerp( horizontalRecoilBias, Rand.Float( -1f, 1f ), Time.Delta * 2f );

		// Apply direct aim climb per shot so recoil is felt as control challenge, not camera wobble.
		WeaponOwner.PlayerController.EyeAngles += new Angles( -pitchKick * 0.55f, yawKick * 0.18f, 0f );

		// Keep a lighter recoil memory for spread/bloom only.
		recoilOffset.pitch = Math.Clamp( recoilOffset.pitch + pitchKick * 0.45f, 0f, 8f );
		recoilOffset.yaw = Math.Clamp( recoilOffset.yaw + yawKick * 0.35f, -1.25f, 1.25f );
		float bloomPerShot = GetBloomPerShot() * (isAutomatic ? 1f : 0.9f);
		if ( WeaponOwner.isAiming )
		{
			bloomPerShot *= 0.75f;
		}
		shotInaccuracy = Math.Clamp( shotInaccuracy + bloomPerShot, 0f, 3.2f );
	}

	[Rpc.Owner]
	private void HandleRecoil()
	{
		float recoverSpeed = WeaponOwner.isAiming ? 10f : 7f;
		recoilOffset = Angles.Lerp( recoilOffset, Angles.Zero, Time.Delta * recoverSpeed );
		recoilVelocity = Angles.Lerp( recoilVelocity, Angles.Zero, Time.Delta * 20f );
	}

	private float GetCurrentSpread( bool isAiming )
	{
		float baseSpread = GetBaseSpread( isAiming );

		float moveSpeed = WeaponOwner.PlayerController.Velocity.WithZ( 0f ).Length;
		float runSpeed = Math.Max( 1f, WeaponOwner.PlayerController.RunSpeed );
		float movementNorm = Math.Clamp( moveSpeed / runSpeed, 0f, 1f );
		float movementSpread = movementNorm * (isAiming ? 0.45f : 1.0f);
		float recoilSpread = Math.Clamp( recoilOffset.pitch * 0.05f, 0f, 0.9f );
		float bloomSpread = shotInaccuracy * (isAiming ? 0.35f : 0.85f);

		return baseSpread + movementSpread + recoilSpread + bloomSpread;
	}

	private float GetRecoilTypeMultiplier()
	{
		switch ( WeaponType )
		{
			case BaseWeapon.weaponType.Pistol: return 0.6f;
			case BaseWeapon.weaponType.Smg: return 0.5f;
			case BaseWeapon.weaponType.Rifle: return 0.75f;
			case BaseWeapon.weaponType.Shotgun: return 2.0f;
			case BaseWeapon.weaponType.Launcher: return 1.4f;
			case BaseWeapon.weaponType.Sniper: return 1.6f;
			case BaseWeapon.weaponType.Special: return 1.1f;
			default: return 1.0f;
		}
	}

	private float GetBloomPerShot()
	{
		switch ( WeaponType )
		{
			case BaseWeapon.weaponType.Pistol: return 0.08f;
			case BaseWeapon.weaponType.Smg: return 0.06f;
			case BaseWeapon.weaponType.Rifle: return 0.10f;
			case BaseWeapon.weaponType.Shotgun: return 0.34f;
			case BaseWeapon.weaponType.Launcher: return 0.3f;
			case BaseWeapon.weaponType.Sniper: return 0.2f;
			case BaseWeapon.weaponType.Special: return 0.18f;
			default: return 0.14f;
		}
	}

	private float GetBaseSpread( bool isAiming )
	{
		switch ( WeaponType )
		{
			case BaseWeapon.weaponType.Pistol: return isAiming ? 0.14f : 1.0f;
			case BaseWeapon.weaponType.Smg: return isAiming ? 0.18f : 1.25f;
			case BaseWeapon.weaponType.Rifle: return isAiming ? 0.12f : 1.05f;
			case BaseWeapon.weaponType.Shotgun: return isAiming ? 1.7f : 3.1f;
			case BaseWeapon.weaponType.Launcher: return isAiming ? 0.6f : 1.8f;
			case BaseWeapon.weaponType.Sniper: return isAiming ? 0.02f : 1.9f;
			case BaseWeapon.weaponType.Special: return isAiming ? 0.2f : 1.35f;
			default: return isAiming ? 0.12f : 1.05f;
		}
	}

	[Rpc.Owner]
	private void DoLineTrace( Vector3 pos, Rotation rot, bool isAiming, bool ShouldDoEffects, bool ShouldGivePoints )
	{
		var cameraPos = pos;
		var direction = rot.Forward;

		if ( !isAiming )
		{
			float spreadAngle = GetCurrentSpread( false ); // max spread in degrees

			// Random offsets
			float pitchOffset = Rand.Float( -spreadAngle, spreadAngle );
			float yawOffset = Rand.Float( -spreadAngle, spreadAngle );

			// Convert Rotation to Angles
			var angles = rot.Angles();
			angles.pitch += pitchOffset;
			angles.yaw += yawOffset;

			// Convert back to Rotation
			var spreadRot = Rotation.From( angles );
			direction = spreadRot.Forward;
		}
		if ( isAiming )
		{
			float spreadAngle = GetCurrentSpread( true ); // max spread in degrees

			// Random offsets
			float pitchOffset = Rand.Float( -spreadAngle, spreadAngle );
			float yawOffset = Rand.Float( -spreadAngle, spreadAngle );

			// Convert Rotation to Angles
			var angles = rot.Angles();
			angles.pitch += pitchOffset;
			angles.yaw += yawOffset;

			// Convert back to Rotation
			var spreadRot = Rotation.From( angles );
			direction = spreadRot.Forward;
		}

		var endPosition = cameraPos + direction * 999999;

		//DebugOverlay.Line( cameraPos, endPosition, Color.Red, 4f );

		var traceResult = Scene.Trace
			.Ray( cameraPos, endPosition )
			.IgnoreGameObject( GameObject )
			.WithoutTags( "winder" )
			.WithoutTags( "capsule" )
			.WithoutTags( "capplay" )
			.UseHitPosition()
			.UseHitboxes( true )
			.RunAll();

		//Log.Info( traceResult.FirstOrDefault().GameObject );

		handleTrace( RemoveDupeHits( traceResult ), ShouldDoEffects, ShouldGivePoints );
	}


	private GameObject GetRoot( GameObject obj )
	{
		var current = obj;
		while ( current.Parent != null )
			current = current.Parent;
		return current;
	}

	private List<SceneTraceResult> RemoveDupeHits( IEnumerable<SceneTraceResult> hits )
	{

		return hits
			.GroupBy( h => GetRoot( h.GameObject ) )   // group by absolute parent
			.Select( g => g.First() )
			.ToList();
	}



	[Rpc.Owner]
	private void handleTrace( IEnumerable<SceneTraceResult> hitResult, bool ShouldDoEffects, bool ShouldGivePoints )
	{

		//var x = RemoveDupeHits( hitResult );

		foreach ( var y in hitResult )
		{
			Log.Info( y.GameObject.Name );
			if ( ShouldDoEffects )
			{
				BulletImpact( y );
			}
			CreateBulletHole( y.EndPosition, y.Normal );
			if ( y.Tags.Contains( "nopen" ) )
			{
				return;
			}

			if ( y.Tags.Contains( "zombie" ) )
			{
				DamageInfo damageInfo = new DamageInfo();

				damageInfo.Attacker = GameObject;
				damageInfo.Damage = WeaponDamage;
				var zombie = y.GameObject.GetComponentInParent<Zombie>();
				if ( zombie == null )
				{
					Log.Info( "zombie null" );

				}

				ApplyDamage( zombie, damageInfo, y.EndPosition, y.Bone, y.Direction, -y.Direction.Normal, GameObject.WorldPosition, y.Normal, y.GameObject );

			}
		}



	}

	[Rpc.Broadcast]
	private static void PlaySoundAtLoc( string sound, Vector3 location )
	{
		Sound.Play( sound, location );
	}


	[Rpc.Broadcast]
	private void CreateBulletImpactEffects( Vector3 location, bool flesh, PrefabFile effect, Vector3 rotat )
	{
		if ( !flesh )
		{
			var gHit = GameObject.GetPrefab( effect.ResourcePath ).Clone( location );
			gHit.WorldRotation = Rotation.LookAt( rotat, Vector3.Up );

			ServerDestroyMS( gHit, 2500 );


		}
		else
		{
			var gHit = GameObject.GetPrefab( effect.ResourcePath ).Clone( location );
			gHit.WorldRotation = Rotation.LookAt( rotat, Vector3.Up );
			ServerDestroyMS( gHit, 2500 );
		}

	}

	[Rpc.Owner]
	private void CreateBulletHole( Vector3 location, Vector3 rotat )
	{
		var decal = ResourceLibrary.Get<DecalDefinition>( "decals/impact/concrete/decal_impact_concrete_01.decal" );
		var gHit = new GameObject();
		gHit.WorldPosition = location;
		gHit.WorldRotation = Rotation.LookAt( rotat, Vector3.Up );
		gHit.AddComponent<Decal>().Decals.Add( decal );
		ServerDestroyMS( gHit, 10000 );
	}


	private void BulletImpact( SceneTraceResult Hr )
	{
		PlaySoundAtLoc( Hr.Surface.Sounds.Bullet, Hr.EndPosition );

		if ( Hr.Tags.Contains( "flesh" ) )
		{
			CreateBulletImpactEffects( Hr.EndPosition, true, currentWeaponData.FleshImpactEffectPrefab, Hr.Normal );

		}
		else
		{
			CreateBulletImpactEffects( Hr.EndPosition, false, currentWeaponData.ImpactEffectPrefab, Hr.Normal );

		}

	}



	private void ApplyDamage( Zombie zombie, DamageInfo damageInfo, Vector3 hitPos, int hitBone, Vector3 Direction, Vector3 LocalDirection, Vector3 StartPos, Vector3 HitNormal, GameObject hitObject )
	{
		Log.Info( "apply damage" );
		zombie.TakeDamage( damageInfo, hitPos, hitBone, Direction, StartPos, HitNormal, hitObject, WeaponPower );
	}



	private void UpdateAmmoSingleBullet()
	{
		WeaponOwner.Inventory.ReduceAmmo( 1 );
		WeaponOwner.Inventory.AddAmmoToMag( 1 );
		WeaponModel.Set( "b_reloading_shell", true );
	}

	private void UpdateAmmo()
	{
		WeaponOwner.Inventory.ReduceAmmo( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax - WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag );
		WeaponOwner.Inventory.AddAmmoToMag( currentWeaponData.MagMax );
	}

	//change to timesince

	private void Reload()
	{
		UpdateAmmo();
		Reloading = false;
		WeaponModel.Set( "b_reloading", false );
	}

	private void StartReload()
	{
		if ( !this.Network.IsOwner ) return;
		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag == WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax || WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].AmmoTotal == 0 ) return;
		Reloading = true;
		WeaponModel.Set( "b_reload", true );

		ReloadTP();
		TimeSinceReloadStarted = 0;
	}

	private void StartSingleBulletReload()
	{
		if ( !Network.IsOwner ) return;

		var ammo = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot];

		if ( ammo.CurrentMag >= ammo.MagMax || ammo.AmmoTotal <= 0 )
		{
			ReloadingSingleBullet = false;
			Reloading = false;
			WeaponModel.Set( "b_reloading_shell", false );
			WeaponModel.Set( "b_reloading", false );
			return;
		}

		ReloadingSingleBullet = true;
		Reloading = true;
		TimeSinceLastBulletReload = 0;

		WeaponModel.Set( "b_reloading", true );
		WeaponModel.Set( "b_reloading_shell", true );

	}

	// Call this every Tick
	private void TickSingleBulletReload( float deltaTime )
	{
		TimeSinceReloadStarted += Time.Delta;

		if ( ReloadingSingleBullet )
		{
			TimeSinceLastBulletReload += Time.Delta;

			// Only add a single shell after 1.5s
			if ( TimeSinceLastBulletReload >= ReloadTime )
			{
				TimeSinceLastBulletReload = 0;
				UpdateAmmoSingleBullet(); // adds one shell

				var ammo = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot];
				if ( ammo.CurrentMag >= ammo.MagMax || ammo.AmmoTotal <= 0 )
				{
					ReloadingSingleBullet = false;
					Reloading = false;
					WeaponModel.Set( "b_reloading_shell", false );
					WeaponModel.Set( "b_reloading", false );

				}
			}
		}

		// Only trigger normal full-mag reload if NOT doing single-bullet reload
		if ( Reloading && !ReloadingSingleBullet && TimeSinceReloadStarted >= ReloadTime )
		{
			Reload();
		}
	}



	[Rpc.Broadcast]
	private void ShootTP()
	{
		WeaponOwner.Body.Set( "b_attack", true );
	}


	[Rpc.Broadcast]
	private void ReloadTP()
	{
		WeaponOwner.Body.Set( "b_reload", true );
	}

	[Rpc.Owner]
	private void PlayEffects()
	{
		if ( WeaponModel.GetAttachment( "muzzle" ) != null )
		{
			var mFlash = MuzzleFlash.Clone( WeaponModel.GetAttachment( "muzzle" ).Value );
			ClientDestroyMS( mFlash, 300 );
		}

		//ThirdpersonEffects( WeaponModel.GetAttachment( "muzzle" ).Value );

	}

	[Rpc.Broadcast]
	private void ThirdpersonEffects( Transform Location )
	{
		//Log.Info( WeaponOwner.ThirdPersonWeaponModel );
		//var mFlash = MuzzleFlash.Clone(Location );
		//mFlash.NetworkSpawn();
	}

	/*[Rpc.Host]
	private void TPEffect()
	{
		ThirdpersonEffects();
	}
*/
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


		WeaponType = wClass.WeaponType;

		ReloadTime = wClass.ReloadTime;

		isAutomatic = wClass.Automatic;

		BoltAction = wClass.BoltAction;
		PumpAction = wClass.PumpAction;

		PumpTime = wClass.PumpTime;
		BoltTime = wClass.BoltTime;


		MagMax = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax;
		CurrentMag = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag;

		FireRate = wClass.FireRate;

		//WeaponRange = wClass.WeaponRange;

		WeaponPower = wClass.WeaponPower;

		WeaponDamage = wClass.WeaponDamage;

		Log.Info( "path: " + wClass.ImpactEffectPrefab.ResourcePath );
		ImpactEffectObject = GameObject.GetPrefab( wClass.ImpactEffectPrefab.ResourcePath );
		Log.Info( "object: " + ImpactEffectObject );

		Log.Info( "path: " + wClass.FleshImpactEffectPrefab.ResourcePath );

		ImpactEffectFlesh = GameObject.GetPrefab( wClass.FleshImpactEffectPrefab.ResourcePath );
		Log.Info( "object: " + ImpactEffectFlesh );

		Log.Info( "path: " + wClass.MuzzleFlashPrefab.ResourcePath );
		MuzzleFlash = GameObject.GetPrefab( wClass.MuzzleFlashPrefab.ResourcePath );
		Log.Info( "object: " + MuzzleFlash );

		Log.Info( "firerate" + FireRate );
	}





	private void HandleInput()
	{

		if ( WeaponOwner.isDeploying || WeaponOwner.isKnifing )
		{
			ReloadingSingleBullet = false;
			Reloading = false;
			UnADS();
			return;
		}



		// ADS
		if ( Input.Down( "Attack2" ) ) ADS();
		if ( Input.Released( "Attack2" ) ) UnADS();

		// Reload
		if ( Input.Pressed( "Reload" ) && !Reloading && !ReloadingSingleBullet )
		{
			if ( WeaponType == BaseWeapon.weaponType.Shotgun )
				StartSingleBulletReload();
			else
				StartReload();
		}

		// Fire
		if ( Input.Down( "Attack1" ) && WeaponOwner.CanShootWeapon )
		{
			if ( WeaponOwner.isKnifing ) return;
			// Cancel reload if firing
			if ( (ReloadingSingleBullet || Reloading) && WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag > 0 )
			{
				ReloadingSingleBullet = false;
				Reloading = false;
				WeaponModel.Set( "b_reloading_shell", false );
				WeaponModel.Set( "b_reloading", false );
			}

			// Do not shoot if no ammo
			if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 )
			{
				WeaponOwner.isFiring = false;
				if ( !ReloadingSingleBullet && WeaponType == BaseWeapon.weaponType.Shotgun )
				{
					StartSingleBulletReload();

				}
				if ( !Reloading && WeaponType != BaseWeapon.weaponType.Shotgun )
				{
					StartReload();
				}
				return; // prevents shooting with zero ammo
			}

			// Automatic fire
			if ( isAutomatic )
			{
				WeaponOwner.isFiring = true;
				Shoot();
			}
			else // semi-auto
			{
				if ( Input.Pressed( "Attack1" ) )
				{
					WeaponOwner.isFiring = true;
					Shoot();
				}
			}
		}
		else
		{
			WeaponOwner.isFiring = false;
		}
	}


	[Rpc.Owner]
	private void HandleAnimEventTags( SceneModel.AnimTagEvent tag )
	{
		Log.Info( tag );
	}


	protected override void OnStart()
	{
		base.OnStart();



	}


	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		base.OnUpdate();
		HandleInput();
		HandleRecoil();
		shotInaccuracy = MathX.Lerp( shotInaccuracy, 0f, Time.Delta * (WeaponOwner.isAiming ? 7f : 5f) );


		/*if ( Reloading || ReloadingSingleBullet )
		{
			WeaponModel.Set( "b_reloading", true );
		}

		if ( !Reloading || ReloadingSingleBullet )
		{
			WeaponModel.Set( "b_reloading", false );
		}
		*/
		TickSingleBulletReload( Time.Delta );

		/*if ( TimeSinceReloadStarted >= ReloadTime && Reloading == true)
		{
			Reload();
			
		}*/

	}
}
