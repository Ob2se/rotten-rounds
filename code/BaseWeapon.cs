using Sandbox;
using Sandbox.Citizen;
using System;
using System.Numerics;
using static Sandbox.Package;
using static Sandbox.VertexLayout;

public sealed class PlayerWeapon : Component
{


	[Property] public SkinnedModelRenderer WeaponModel { get; set; }

	[Property] public PrefabFile MuzzleFlashPrefab { get; set; }

	[Property] public PrefabFile ImpactEffectPrefab { get; set; }

	

	[Property] public PrefabFile FleshImpactEffectPrefab { get; set; }

	private GameObject ImpactEffectObject { get; set; }

	private GameObject ImpactEffectFlesh { get; set; }
	private GameObject MuzzleFlash { get; set; }

	[Property] 
	GameObject weaponContainer { get; set; }

	[Sync, Property] bool Automatic { get; set; }

	[Property] CitizenAnimationHelper WeaponAnimationHelper { get; set; }

	[Sync, Property]
	public float FireRate { get; set; }

	[Sync, Property]
	public int MagMax { get; set; }

	[Sync] public int CurrentAmmo { get; set; }

	[Sync, Property] public int CurrentMag { get; set; }


	[Sync, Property]
	public int AmmoMax { get; set; }

	[Property]
	public int WeaponRange { get; set; }

	[Sync, Property]
	public int WeaponDamage { get; set; }

	private TimeSince TimeSinceLastShot;

	[Property]
	private int ReloadTime { get; set; }

	private bool Reloading = false;

	Player WeaponOwner { get; set; }


	public enum weaponType {
		Pistol = 0,
		Smg = 1,
		Rifle = 2,
		Shotgun = 3,
		Lmg = 4
	}

	[Property]
	public weaponType WeaponType { get; set; }

	/*private void WeaponMeshChanged(Model oldValue, Model newValue)
	{
		WeaponModel = weaponContainer.AddComponent<SkinnedModelRenderer>();
		WeaponModel.Model = newValue;
	}*/


	protected override void OnStart()
	{
		base.OnStart();
		WeaponOwner = GetComponentInParent<Player>();

		MuzzleFlash = GameObject.GetPrefab( MuzzleFlashPrefab.ResourcePath );
		ImpactEffectObject = GameObject.GetPrefab( ImpactEffectPrefab.ResourcePath );
		ImpactEffectFlesh = GameObject.GetPrefab( FleshImpactEffectPrefab.ResourcePath );

		if ( !this.Network.IsOwner )
		{
			WeaponModel.Enabled = false;
		}

		SetInitialAmmo();

	}



	[Rpc.Host]
	private void SetInitialAmmo()
	{
		CurrentAmmo = MagMax;
	}


	[Rpc.Broadcast]
	private void UpdateAmmo()
	{
		var ammo1 = MagMax - CurrentMag;
		CurrentAmmo = CurrentAmmo - ammo1;
		CurrentMag = MagMax;
	}


	private async void Reload()
	{

		if ( !this.Network.IsOwner ) return;
		if ( CurrentMag == MagMax ) return;
		Reloading = true;
		UpdateAmmo();
		WeaponModel.Set( "b_reload", true );
		await Task.Delay( ReloadTime );
		Reloading = false;
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

	private void UnADS()
	{
		if ( !this.Network.IsOwner ) return;
		WeaponModel.Set( "ironsights", 0 );
	}


	private void ADS()
	{
		if ( !this.Network.IsOwner ) return;
		WeaponModel.Set( "ironsights", 1 );
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
		if ( TimeSinceLastShot < 1f / FireRate ) return;

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
	private void DoLineTrace(Vector3 pos, Rotation rot)
	{
		

		var cameraPos = pos;
		var direction = rot.Forward;

		var endPosition = cameraPos + direction * WeaponRange;
		//DebugOverlay.Line( cameraPos, endPosition, Color.Red, 4f );
		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).IgnoreGameObject( GameObject.Parent.Parent.Parent.Parent.Parent ).WithoutTags("winder").RunAll(); //should probably limit the amount of zombies/objects/thickness of things but fuck it for now
		foreach ( var hit in traceResult )
		{
			if ( hit.Hit )
			{
				handleTrace( hit );
				BulletImpact( hit );
			}
		}
		
	}

	[Rpc.Host]
	private void handleTrace( SceneTraceResult hitResult )
	{
			if ( hitResult.Tags.Contains("zombie")  )
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
	private static void PlaySoundAtLoc(string sound, Vector3 location)
	{
		Sound.Play( sound, location );
	}


	[Rpc.Broadcast]
	private void CreateBulletImpactEffects(Vector3 location, bool flesh)
	{
		if ( !flesh )
		{

			var gHit = ImpactEffectObject.Clone( location );
			ServerDestroyMS( gHit, 300 );

		}
		else {
			var gHit = ImpactEffectFlesh.Clone( location );
			ServerDestroyMS( gHit, 300 );
		}

	}



	[Rpc.Host]
	private void BulletImpact( SceneTraceResult Hr )
	{
		PlaySoundAtLoc(Hr.Surface.Sounds.Bullet, Hr.EndPosition); //bug here
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


	private void Recoil()
	{
		//recoil logic here, simple up with varying strengths depending on the weapon
	}



	private void HandleInputs()
	{


		if ( Automatic )
		{
			if ( Input.Down( "Attack1" ) )
			{
				Shoot();
			}
		}
		else
		{
			if ( Input.Pressed( "Attack1" ) )
			{
				Shoot();
			}
		}

		if ( Input.Down( "Reload" ))
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



	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		base.OnUpdate();
		HandleInputs();
	}
}
