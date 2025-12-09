using Sandbox;
using System;
using System.Dynamic;
using System.Numerics;
using System.Threading.Tasks;

public sealed class Zombie : Component
{



	[Sync, Property] public SimpleZombieController SimpleZombieController { get; set; }

	[Sync, Property, Change("TookDamage")] public float Health { get; set; }

	public bool InstaKillActivated = false;

	public bool DoublePointsActivated = false;

	[Property]
	public List<GameObject> ZombieLimbs { get; set; }

	[Property]
	public SkinnedModelRenderer MasterModel {  get; set; }

	[Property]
	SoundEvent ZombieMoans { get; set; }

	[Property]
	public SkinnedModelRenderer zombieModel { get; set; }

	[Property]
	Collider ZombieCollider { get; set; }

	[Property]
	ModelPhysics rag { get; set; }

	[Property]
	public Dictionary<string, GameObject> Gibbin { get; set; }


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
	public void TakeDamage( DamageInfo DamageInfo, Vector3 hitPos, int hitBone, Vector3 Direction, Vector3 LocalDirection, Vector3 startPos, Vector3 hitNormal, GameObject hitObject )
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
				switch(hitObject.Name)
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
				}


				ZombieDead();
				Log.Info( "hitbone: " + hitObject );
				ZombieRagdoll(hitObject, hitPos, dir);
				ControlZombieController();
				
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


	[Rpc.Host]
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

		foreach ( var kvp in gibMap )
		{
			if ( hitObject.Tags.Has( kvp.Key ) )
			{

				zombieModel.SetBodyGroup( "Dismemberment", kvp.Value );
				if ( kvp.Key == "head" ) return;
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
		if ( Health <= 0 && isAlive)
		{
			
			
		}

	}


	[Rpc.Host]
	public void ZombieDead()
	{
		isAlive = false;
		Scene.RunEvent<IZombieHandler>( x => x.ZombieDeath() );
		//ZombieRagdoll();
		if ( zombieMoanHandle != null )
		{
			zombieMoanHandle.Stop(0);
		}

		if ( zombieAttackHandle != null )
		{
			zombieAttackHandle.Stop( 0 );
		}
		if ( OneIn( 1 ) )
		{
			var pUp = Scene.GetPrefab( "prefabs/powerups/powerup.prefab" );
			var powerup = pUp.Clone( new Vector3(GameObject.WorldPosition.x, GameObject.WorldPosition.y, GameObject.WorldPosition.z + 30) );
			powerup.NetworkSpawn();
		}
		DestroyZombieMS( 6500 );
		
	}

	


	[Rpc.Broadcast]
	private async void DestroyZombieMS(int ms)
	{
		await Task.Delay( ms );
		GameObject.Destroy();
	}

	//use new networked ragdolls ig 
	[Rpc.Broadcast]
	public void ZombieRagdoll(GameObject hitBone, Vector3 hitPos, Vector3 impulseDir)
	{

		ZombieCollider.Enabled = false;

		_ = RagdollIt( hitPos, impulseDir, hitBone );

	}

	private async Task RagdollIt(Vector3 hitPos, Vector3 impulseDir, GameObject hitBone)
	{
		await Task.Delay( 50 );


		rag.Enabled = true;

		rag.CopyBonesFrom( zombieModel, false );

		var x = GameObject.Children.FirstOrDefault( child => child.Name == "Capsule" );
		var y = x.AddComponent<Rigidbody>();



		foreach(var body in rag.Bodies)
		{
			if ( hitBone != null )
			{
				
				if ( body.Component.GameObject.Name == hitBone.Name )
				{
					Log.Info("match found applying impulse");
					var force = impulseDir * 20000f;
					body.Component.ApplyImpulseAt( hitPos, force );
				}
			}
			else if ( hitBone == null )
			{
				MasterModel.AddComponent<Rigidbody>().ApplyImpulseAt( hitPos, impulseDir * 20000f );
			}
		}

		rag.MotionEnabled = true;

		
		zombieModel.UseAnimGraph = false;



		GameObject.Tags.Remove( "zombie" );
		GameObject.Tags.Add( "zombieDead" );

	}


	private async Task DisableRagdoll(ModelPhysics ragdoll)
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
		foreach(var x in ZombieLimbs)
		{
			if(MasterModel.GetBoneObject(x.Name) != null )
			{
				x.SetParent(MasterModel.GetBoneObject(x.Name), true);
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
		zombieMoanHandle = Sound.Play(ZombieMoans, GameObject.WorldPosition );
	}


	protected override void OnStart()
	{

		


		if ( !Networking.IsHost ) return;

		
		//GameModeManaga = Scene.Directory.FindByName( "GameModeManager" ).First().GetComponent<GameModeManager>();
	}


	protected override void OnUpdate()
	{
		//SyncAnimations();

		if(TimeSinceLastMoan >= TimeTillNextMoan && isAlive)
		{
			//PlayMoanSound();
			TimeTillNextMoan = Rand.Int( 10, 18 );
			TimeSinceLastMoan = 0;
		}


	}
}
