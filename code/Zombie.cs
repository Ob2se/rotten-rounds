using Sandbox;
using System;
using System.Dynamic;
using System.Numerics;
using System.Threading.Tasks;
using static Sandbox.ModelPhysics;

public sealed class Zombie : Component
{



	[Sync, Property] public SimpleZombieController SimpleZombieController { get; set; }

	[Sync, Property, Change( "TookDamage" )] public float Health { get; set; }

	public bool InstaKillActivated = false;

	public bool DoublePointsActivated = false;

	[Property]
	public List<GameObject> ZombieLimbs { get; set; }

	[Property]
	public SkinnedModelRenderer MasterModel { get; set; }

	[Property]
	SoundEvent ZombieMoans { get; set; }

	[Property]
	public SkinnedModelRenderer zombieModel { get; set; }

	[Property]
	Collider ZombieCollider { get; set; }

	[Property]
	Collider ZombieCollider2 { get; set; }

	[Sync, Property]
	ModelPhysics rag { get; set; }

	[Property]
	public Dictionary<string, GameObject> Gibbin { get; set; }


	[Property]
	public PrefabFile zombieRagdollPrefab { get; set; }

	private Random Rand { get; set; } = new Random();

	[Property]
	Rigidbody ZombieRigidbody { get; set; }

	public bool isAlive = true;

	public static event Action ZombieDied;



	TimeSince TimeSinceLastMoan = 0f;

	float TimeTillNextMoan = 15f;

	SoundHandle zombieMoanHandle { get; set; }

	SoundHandle zombieAttackHandle { get; set; }



	private static readonly Random Rando = new();

	public static bool OneIn( int x )
	{
		if ( x <= 1 )
			return true; // If someone passes 0 or 1, it's guaranteed true

		return Rando.Next( x ) == 0;
	}


	[Rpc.Host]
	public void TakeKnifeDamage( Player player, Vector3 hitPos, Vector3 impulseDir, string hitBone, float WeaponPower )
	{
		Log.Info( "knife dmaaaagegeagag" );
		if ( SimpleZombieController.CurrentState != SimpleZombieController.ZomState.Dead )
		{
			RemoveHealth( 50 );
			if ( Health <= 0 )
			{
				GivePlayerPoints( player.GameObject, 120 );
				ZombieDead();
				RagdollIt( hitPos, impulseDir, hitBone ?? string.Empty, WeaponPower );
				ControlZombieController();
			}
			else
			{
				if ( DoublePointsActivated )
				{
					GivePlayerPoints( player.GameObject, 20 );
					return;
				}
				GivePlayerPoints( player.GameObject, 10 );
			}
		}
	}

	[Rpc.Host]
	public void TakeDamage( DamageInfo DamageInfo, Vector3 hitPos, int hitBone, Vector3 Direction, Vector3 startPos, Vector3 hitNormal, GameObject hitObject, float WeaponPower )
	{
		if ( SimpleZombieController.CurrentState != SimpleZombieController.ZomState.Dead )
		{
			RemoveHealth( DamageInfo.Damage );

			var incDir = (hitPos - startPos).Normal;
			var dir = (incDir - hitNormal * 1f).Normal;

			zombieModel.Set( "hit_bone", hitBone );
			zombieModel.Set( "hit_direction", dir );

			zombieModel.Set( "hit_strength", .2f );
			zombieModel.Set( "hit", true );

			if ( Health <= 0 )
			{
				if ( hitObject != null )
				{
					GibbedUp( hitObject, hitBone, hitPos, Direction );
				}
				switch ( hitObject.Name )
				{
					case "head":
						if ( DoublePointsActivated )
						{
							GivePlayerPoints( DamageInfo.Attacker, 200 );
							break;
						}
						GivePlayerPoints( DamageInfo.Attacker, 100 );
						break;
					case "spine_0":
						if ( DoublePointsActivated )
						{
							GivePlayerPoints( DamageInfo.Attacker, 120 );
							break;
						}
						GivePlayerPoints( DamageInfo.Attacker, 60 );
						break;
					default:
						if ( DoublePointsActivated )
						{
							GivePlayerPoints( DamageInfo.Attacker, 120 );
							break;
						}
						GivePlayerPoints( DamageInfo.Attacker, 60 );
						break;
				}

				ZombieDead();
				Log.Info( "hitbone: " + hitObject );

				ControlZombieController();


				RagdollIt( hitPos, dir, hitObject?.Name ?? string.Empty, WeaponPower );


			}
			else
			{
				if ( DoublePointsActivated )
				{
					GivePlayerPoints( DamageInfo.Attacker, 20 );
					return;
				}
				GivePlayerPoints( DamageInfo.Attacker, 10 );
			}
		}
	}

	[Rpc.Host]
	private void GivePlayerPoints( GameObject player, int points )
	{
		if ( player != null )
		{
			var PlayerClass = player.GetComponentInChildren<Player>();
			PlayerClass.AddPoints( points );
			Log.Info( PlayerClass.Points );
		}
	}


	
	private void GibbedUp( GameObject hitObject, int hitBone, Vector3 HitPos, Vector3 Direction )
	{
		var gibMap = new Dictionary<string, int>
		{
			{ "head", 1 },
			{ "larm", 2 },
			{ "rarm", 3 },
			{ "lleg", 6 },
			{ "rleg", 7 }
		};
		Log.Info( "GibbedUp: " + hitObject);
		foreach ( var kvp in gibMap )
		{
			if ( hitObject.Tags.Has( kvp.Key ) )
			{
				Log.Info( "GibbedUp: dismemberment at: " + kvp.Key + "|" + kvp.Value );
				zombieModel.SetBodyGroup( "Dismemberment", kvp.Value );

				var c = Gibbin.FirstOrDefault( gib => gib.Key == kvp.Key ).Value;
				c.Enabled = true;

				var z = c.GetComponents<ModelPhysics>();
				foreach ( var m in z )
				{
					m.CopyBonesFrom( zombieModel, false );
					m.Bodies.ForEach( body => body.Component.ApplyImpulseAt( HitPos, Direction * 1000f ) );
				}
				break;
				
				

				
			}
		}
	}


	[Rpc.Host]
	private void RemoveHealth( float damage )
	{
		Health -= damage;
	}


	private void TookDamage()
	{
		if ( Health <= 0 && isAlive )
		{
		}
	}


	[Rpc.Host]
	public void ZombieDead()
	{
		isAlive = false;
		Scene.RunEvent<IZombieHandler>( x => x.ZombieDeath() );
		if ( zombieMoanHandle != null )
		{
			zombieMoanHandle.Stop( 0 );
		}

		if ( zombieAttackHandle != null )
		{
			zombieAttackHandle.Stop( 0 );
		}
		if ( OneIn( 100 ) )
		{
			var pUp = Scene.GetPrefab( "prefabs/powerups/powerup.prefab" );
			var powerup = pUp.Clone( new Vector3( GameObject.WorldPosition.x, GameObject.WorldPosition.y, GameObject.WorldPosition.z + 30 ) );
			powerup.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 30;
			powerup.NetworkSpawn();
		}
		DestroyZombieMS( 2000 );
	}


	private async Task MeleeRagdoll()
	{
		await Task.Delay( 50 );

		rag.Enabled = true;

		rag.CopyBonesFrom( zombieModel, false );

		var x = GameObject.Children.FirstOrDefault( child => child.Name == "Capsule" );
		var y = x.AddComponent<Rigidbody>();

		rag.MotionEnabled = true;

		zombieModel.UseAnimGraph = false;

		GameObject.Tags.Remove( "zombie" );
		GameObject.Tags.Add( "zombieDead" );
	}


	[Rpc.Broadcast]
	private async void DestroyZombieMS( int ms )
	{
		await Task.Delay( ms );
		GameObject.Destroy();
	}


	[Rpc.Broadcast]
	public void ZombieRagdoll( string hitBoneName, Vector3 hitPos, Vector3 impulseDir, float WeaponPower )
	{


		// disable collider locally
		try
		{
			if ( ZombieCollider != null ) ZombieCollider.Enabled = false;
		}
		catch ( Exception e )
		{
			Log.Warning( "Failed to disable ZombieCollider: " + e );
		}


		//zombrag.NetworkSpawn();

	}

	[Rpc.Broadcast]
	public void EnabledRag()
	{

	}


	[Rpc.Broadcast]
	private void RagdollIt( Vector3 hitPos, Vector3 impulseDir, string hitBoneName, float WeaponPower )
	{
		ZombieCollider.Enabled = false;
		ZombieCollider2.Enabled = false;

		foreach ( var hitbox in GameObject.GetComponentsInChildren<ManualHitbox>() )
		{
			hitbox.GameObject.Enabled = false;
		}


		var zombrag = GameObject.GetPrefab( zombieRagdollPrefab.ResourcePath );
		var ragdollClone = zombrag.Clone( this.GameObject.WorldPosition );
		var rago = ragdollClone.GetComponentInChildren<ModelPhysics>();

		zombieModel.UseAnimGraph = false;
		MasterModel.UseAnimGraph = false;
		rago.CopyBonesFrom( zombieModel, true );

		//await Task.Delay(50);

		zombieModel.Enabled = false;
		GameObject.GetComponent<Dresser>().Clear();
		rago.MotionEnabled = true;

		// await Task.Delay(50);

		if ( rago.Bodies.Count == 0 )
		{
			Log.Warning( "RagdollIt: No ragdoll bodies found" );
			return;
		}

		var impulseApplied = false;
		foreach ( var body in rago.Bodies )
		{
			Log.Info( $"RagdollIt: body found: {body.Component.GameObject.Name}" );

			if ( !string.IsNullOrEmpty( hitBoneName ) && body.Component.GameObject.Name == hitBoneName )
			{
				Log.Info( "RagdollIt: match found applying impulse to " + hitBoneName );
				var force = impulseDir * 6500;
				body.Component.ApplyImpulseAt( hitPos, force );
				impulseApplied = true;
				break;
			}
		}

		if ( !impulseApplied && rago.Bodies.Count > 0 )
		{
			Log.Info( "RagdollIt: no specific bone matched — applying impulse to first body" );
			var force = impulseDir * WeaponPower;
			rago.Bodies[0].Component.ApplyImpulseAt( hitPos, force );
		}

		GameObject.Tags.Remove( "zombie" );
		GameObject.Tags.Add( "zombieDead" );

	}



	[Rpc.Broadcast]
	public void ApplyImpulseOnZombie( Vector3 hitPos, Vector3 force, int Boneindex )
	{


	}


	private async Task DisableRagdoll( ModelPhysics ragdoll )
	{
		await Task.Delay( 2000 );
		//ragdoll.Enabled = false;
		ZombieRigidbody.Enabled = false;
		//ragdoll.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;
	}


	[Rpc.Broadcast]
	private void ControlZombieController()
	{
		SimpleZombieController.ChangeState( SimpleZombieController.ZomState.Dead );
	}



	private void SyncAnimations()
	{
		foreach ( var x in ZombieLimbs )
		{
			if ( MasterModel.GetBoneObject( x.Name ) != null )
			{
				x.SetParent( MasterModel.GetBoneObject( x.Name ), true );
			}
		}
	}

	[Rpc.Broadcast]
	public void PlayAttackSound()
	{
		zombieAttackHandle = Sound.Play( "sound/zombies/zombieattacks.sound", GameObject.WorldPosition );
	}


	[Rpc.Broadcast]
	public void PlayMoanSound()
	{
		zombieMoanHandle = Sound.Play( ZombieMoans, GameObject.WorldPosition );
	}


	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
	}


	protected override void OnUpdate()
	{
		//SyncAnimations();

		if ( TimeSinceLastMoan >= TimeTillNextMoan && isAlive )
		{
			PlayMoanSound();
			TimeTillNextMoan = Rand.Int( 10, 18 );
			TimeSinceLastMoan = 0;
		}
	}
}
