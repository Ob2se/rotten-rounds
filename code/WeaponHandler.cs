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


	private Queue<Rotation> RecoilQueue = new();


	bool isAutomatic;

	float ReloadTime;

	int CurrentMag;

	bool Reloading;

	float FireRate;

	int WeaponRange;

	int WeaponDamage;

	int AmmoMax;

	int MagMax;

	float WeaponPower;

	BaseWeapon.weaponType WeaponType;



	float RecoilStrength = 10f;


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

	private Random Rand = new Random();

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


	[Rpc.Owner]
	private void Shoot()
	{
		
		//if ( !this.Network.IsOwner ) return;

		

		if ( Reloading ) return;


		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 && WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].AmmoTotal <= 0 ) return;

		if ( WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag <= 0 )
		{
			Reload();
		}


		float secondsPerShot = 60f / FireRate;
		if ( TimeSinceLastShot < secondsPerShot ) return;

		Log.Info( "aw shhoot" );

		if ( WeaponType == BaseWeapon.weaponType.Shotgun )
		{
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
		}
		else
		{
			DoLineTrace( WeaponOwner.playerCamera.WorldPosition, WeaponOwner.playerCamera.WorldRotation, WeaponOwner.isAiming );
		}



			ServerReduceAmmo( 1 );
		WeaponModel.Set( "b_attack", true );
		WeaponOwner.GetComponentInParent<Player>().fpsArms.Set( "b_attack", true );
		PlayEffects();
		ShootTP();

		AddRecoil( RecoilStrength );
		
		//TPEffect();
		var idek = WeaponOwner.ThirdPersonWeaponModel.GetAttachmentObject( "muzzle" ).WorldTransform;
		ThirdpersonEffects(idek);
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
		var cam = WeaponOwner.PlayerController.EyeAngles;

		// Create new recoil for this shot
		Angles recoil = new Angles( -1, 0, 0 ); // vertical only, add yaw if needed
		RecoilQueue.Enqueue( recoil );
	}

	[Rpc.Owner]
	private void HandleRecoil()
	{
		var cam = WeaponOwner.PlayerController.EyeAngles;

		// If no target, take the next one from the queue
		if ( recoilTarget == Angles.Zero && RecoilQueue.Count > 0 )
		{
			recoilTarget = RecoilQueue.Dequeue();
		}

		if ( recoilTarget != Angles.Zero )
		{
			// Smoothly move currentRecoil toward recoilTarget
			float maxStep = 35f * Time.Delta;
			currentRecoil.pitch = MathX.Approach( currentRecoil.pitch, recoilTarget.pitch, maxStep );
			currentRecoil.yaw = MathX.Approach( currentRecoil.yaw, recoilTarget.yaw, maxStep );

			// Apply ONLY the delta since last frame
			cam += currentRecoil - currentRecoilLastFrame;
			cam.pitch = Math.Clamp( cam.pitch, -89f, 89f );

			WeaponOwner.PlayerController.EyeAngles = cam;

			// If we’ve basically reached the target, reset
			if ( Math.Abs( currentRecoil.pitch - recoilTarget.pitch ) < 0.01f &&
				Math.Abs( currentRecoil.yaw - recoilTarget.yaw ) < 0.01f )
			{
				recoilTarget = Angles.Zero;
				currentRecoil = Angles.Zero;
			}
		}

		// Save last frame applied recoil
		currentRecoilLastFrame = currentRecoil;
	}

	[Rpc.Owner]
	private void DoLineTrace( Vector3 pos, Rotation rot, bool isAiming )
	{
		var cameraPos = pos;
		var direction = rot.Forward;

		if ( !isAiming )
		{
			float spreadAngle = 2f; // max spread in degrees

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
			float spreadAngle = .2f; // max spread in degrees

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

		DebugOverlay.Line( cameraPos, endPosition, Color.Red, 4f );

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

		handleTrace( RemoveDupeHits( traceResult ) );
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
	private void handleTrace( IEnumerable<SceneTraceResult> hitResult )
	{

		//var x = RemoveDupeHits( hitResult );

		foreach(var y in hitResult)
		{
			BulletImpact( y );

			if(y.Tags.Contains("nopen"))
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
				
				ApplyDamage( zombie, damageInfo, y.EndPosition, y.Bone, y.Direction, -y.Direction.Normal, WeaponModel.GetAttachment( "muzzle" ).Value.Position, y.Normal, y.GameObject );

			}
		}


		
	}

	[Rpc.Broadcast]
	private static void PlaySoundAtLoc( string sound, Vector3 location )
	{
		Sound.Play( sound, location );
	}


	[Rpc.Broadcast]
	private void CreateBulletImpactEffects( Vector3 location, bool flesh, PrefabFile effect, Vector3 rotat)
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


	private void BulletImpact( SceneTraceResult Hr )
	{
		PlaySoundAtLoc( Hr.Surface.Sounds.Bullet, Hr.EndPosition ); 
		
		if ( Hr.Tags.Contains( "flesh" ) )
		{
			CreateBulletImpactEffects( Hr.EndPosition, true , currentWeaponData.FleshImpactEffectPrefab, Hr.Normal );
			
		}
		else
		{
			CreateBulletImpactEffects( Hr.EndPosition, false, currentWeaponData.ImpactEffectPrefab, Hr.Normal );
			
		}

	}


	
	private void ApplyDamage( Zombie zombie, DamageInfo damageInfo, Vector3 hitPos, int hitBone, Vector3 Direction, Vector3 LocalDirection, Vector3 StartPos, Vector3 HitNormal, GameObject hitObject )
	{
		Log.Info( "apply damage" );
		zombie.TakeDamage( damageInfo, hitPos, hitBone, Direction, LocalDirection, StartPos, HitNormal, hitObject );
	}




	
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
		ReloadTP();
		TimeSinceReloadStarted = 0;
		
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
		var mFlash = MuzzleFlash.Clone( WeaponModel.GetAttachment( "muzzle" ).Value );
		ClientDestroyMS( mFlash, 300 );
		ThirdpersonEffects( WeaponModel.GetAttachment( "muzzle" ).Value );
		
	}

	[Rpc.Broadcast]
	private void ThirdpersonEffects(Transform Location)
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
		//Log.Info( "hellllloo" );

		WeaponOwner = GameObject.GetComponent<Player>();
		

		var wClass = weapon.GetComponentInChildren<BaseWeapon>();
		currentWeaponData = wClass;

		WeaponModel = wClass.WeaponModel;


		WeaponType = wClass.WeaponType;

		ReloadTime = wClass.ReloadTime;

		isAutomatic = wClass.Automatic;

		MagMax = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].MagMax;
		CurrentMag = WeaponOwner.Inventory.WeaponsAmmo[WeaponOwner.CurrentWeaponSlot].CurrentMag;

		FireRate = wClass.FireRate;

		//WeaponRange = wClass.WeaponRange;

		WeaponDamage = wClass.WeaponDamage;

		//Log.Info( "path: " + wClass.ImpactEffectPrefab.ResourcePath );
		ImpactEffectObject = GameObject.GetPrefab( wClass.ImpactEffectPrefab.ResourcePath );
		//Log.Info("object: " + ImpactEffectObject );

		//Log.Info("path: " + wClass.FleshImpactEffectPrefab.ResourcePath );

		ImpactEffectFlesh = GameObject.GetPrefab( wClass.FleshImpactEffectPrefab.ResourcePath );
		//Log.Info( "object: " + ImpactEffectFlesh );

		//Log.Info("path: " + wClass.MuzzleFlashPrefab.ResourcePath );
		MuzzleFlash = GameObject.GetPrefab( wClass.MuzzleFlashPrefab.ResourcePath );
		//Log.Info( "object: " + MuzzleFlash );


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
			if(!isAutomatic)
			{
				if ( Input.Down( "Attack1" ) )
				{
					if ( !WeaponOwner.isFiring )
					{
						TimeSinceShotHeld = 0f;
					}
					WeaponOwner.isFiring = true;
					
					Shoot();
					
					if ( TimeSinceShotHeld > 0f )
					{
						Log.Info( "shoudl stop!" );
						Input.ReleaseAction( "Attack1" );
						WeaponOwner.isFiring = false;
					}

				}
				/*if(TimeSinceLastShot > .1f)
				{
					WeaponOwner.isFiring = false;
				}*/
				if ( Input.Released( "Attack1" ) )
				{
					WeaponOwner.isFiring = false;
				}


			}
		}else if( Reloading )
		{
			WeaponOwner.isFiring = false;
		}

		if ( Input.Down( "Reload" ) && !Reloading)
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
		HandleRecoil();


		/*// Smooth return to normal
		aimPitchInertia = MathX.Lerp( aimPitchInertia, 0f, Time.Delta * 8f );
		aimYawInertia = MathX.Lerp( aimYawInertia, 0f, Time.Delta * 8f );

		var ang = WeaponOwner.PlayerController.EyeAngles;
		ang.pitch += aimPitchInertia;
		ang.yaw += aimYawInertia;
		WeaponOwner.PlayerController.EyeAngles = ang;

		lastEyeAngles = ang;*/

		if ( TimeSinceReloadStarted >= ReloadTime && Reloading == true)
		{
			Reloading = false;
		}

	}
}
