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



	public GameModeManager GameModeManager { get; set; }


	TimeSince TimeSinceLastMoan = 0f;

	float TimeTillNextMoan = 15f;


	[Property]
	SoundPointComponent zombieMoanHandle { get; set; }

	[Property]
	SoundPointComponent zombieAttackHandle { get; set; }



	private static readonly Random Rando = new();

	public static bool OneIn( int x )
	{
		if ( x <= 1 )
			return true; // If someone passes 0 or 1, it's guaranteed true

		return Rando.Next( x ) == 0;
	}


	[Rpc.Host]
	public void TakeKnifeDamage( Player player, Vector3 hitPos, Vector3 impulseDir, string hitBone, float WeaponPower, long steamid, GameObject hitObject )
	{

		if ( SimpleZombieController.CurrentState != SimpleZombieController.ZomState.Dead && isAlive )
		{
			RemoveHealth( 50 );
			if ( Health <= 0 )
			{
				player.UpdateKills();
				GivePlayerPoints( player.GameObject, 120 );
				ZombieDead();
				RagdollIt( hitPos, impulseDir, hitBone ?? string.Empty, WeaponPower, hitObject );
				ControlZombieController();
			}
			else
			{
				if ( Scene.GetAllComponents<GameModeManager>().FirstOrDefault().DoublePointsStarted)
				{
					GivePlayerPoints( player.GameObject, 20 );
					return;
				}
				GivePlayerPoints( player.GameObject, 10 );
			}
		}
	}

	[Rpc.Host]
	public void TakeDamage( DamageInfo DamageInfo, Vector3 hitPos, int hitBone, Vector3 Direction, Vector3 startPos, Vector3 hitNormal, GameObject hitObject, float WeaponPower, long steamid )
	{
		if ( SimpleZombieController.CurrentState != SimpleZombieController.ZomState.Dead && isAlive )
		{
			// Always apply damage, even if hitObject is null (e.g. collider became invalid over the network)
			if ( hitObject != null && hitObject.Name == "head" )
			{
				RemoveHealth( DamageInfo.Damage * 2 );
			}
			else
			{
				RemoveHealth( DamageInfo.Damage );
			}

			var incDir = (hitPos - startPos).Normal;
			var dir = (incDir - hitNormal * 1f).Normal;

			zombieModel.Set( "hit_bone", hitBone );
			zombieModel.Set( "hit_direction", dir );

			zombieModel.Set( "hit_strength", .2f );
			zombieModel.Set( "hit", true );

			if ( Health <= 0 )
			{
				// Determine hit location name safely (null hitObject = body shot)
				var hitName = hitObject?.Name ?? "body";

				switch ( hitName )
				{
					case "head":
						if ( Scene.GetAllComponents<GameModeManager>().FirstOrDefault().DoublePointsStarted )
						{
							GivePlayerPoints( DamageInfo.Attacker, 200 );
							break;
						}
						GivePlayerPoints( DamageInfo.Attacker, 100 );

						DamageInfo.Attacker.GetComponentInChildren<Player>().UpdateHeadshotKills();
						break;
					default:
						if ( Scene.GetAllComponents<GameModeManager>().FirstOrDefault().DoublePointsStarted )
						{
							GivePlayerPoints( DamageInfo.Attacker, 120 );
							break;
						}
						GivePlayerPoints( DamageInfo.Attacker, 60 );
						break;

				}

				ZombieDead();

				DamageInfo.Attacker.GetComponentInChildren<Player>().UpdateKills();
				if ( GameModeManager.KillList.ContainsKey( DamageInfo.Attacker.Network.Owner.SteamId ) )
				{
					GameModeManager.KillList[DamageInfo.Attacker.Network.Owner.SteamId] += 1;
				}
				else
				{
					GameModeManager.KillList.Add( DamageInfo.Attacker.Network.Owner.SteamId, 1 );
				}

				ControlZombieController();


				RagdollIt( hitPos, dir, hitObject?.Name ?? string.Empty, WeaponPower, hitObject );


			}
			else
			{
				if ( Scene.GetAllComponents<GameModeManager>().FirstOrDefault().DoublePointsStarted )
				{
					GivePlayerPoints( DamageInfo.Attacker, 20 );
				}
				else
				{
					GivePlayerPoints( DamageInfo.Attacker, 10 );
				}

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
			if ( GameModeManager.PointsGainedList.ContainsKey( player.Network.Owner.SteamId ) )
			{
				GameModeManager.PointsGainedList[player.Network.Owner.SteamId] += points;
			}
			else
			{
				GameModeManager.PointsGainedList.Add( player.Network.Owner.SteamId, points );
			}
		}
	}


	
	private void GibbedUp( GameObject hitObject, int hitBone, Vector3 HitPos, Vector3 Direction )
	{
		
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

	[Rpc.Broadcast]
	public void BroadcastStopSounds()
	{
		zombieMoanHandle.StopSound();
		zombieMoanHandle.Enabled = false;


		zombieAttackHandle.StopSound();
		zombieAttackHandle.Enabled = false;
	}


	[Rpc.Host]
	public void ZombieDead()
	{
		if ( !isAlive ) return; // Already dead — prevent double-counting
		isAlive = false;
		Scene.RunEvent<IZombieHandler>( x => x.ZombieDeath() );

		BroadcastStopSounds();
		

		if ( OneIn( 100 ) && SimpleZombieController.HasEnteredThroughWindow)
		{
			var pUp = Scene.GetPrefab( "prefabs/powerups/powerup.prefab" );
			var powerup = pUp.Clone( new Vector3( GameObject.WorldPosition.x, GameObject.WorldPosition.y, GameObject.WorldPosition.z + 30 ) );
			powerup.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 30;
			powerup.NetworkSpawn();
		}
		DestroyZombieMS();
	}


	[Rpc.Host]
	public void NukeZombie()
	{
		if ( !isAlive ) return;
		isAlive = false;
		Scene.RunEvent<IZombieHandler>( x => x.ZombieDeath() );
		BroadcastStopSounds();

		var zombrag = zombieRagdollPrefab != null ? GameObject.GetPrefab( zombieRagdollPrefab.ResourcePath ) : null;
		if ( zombrag != null )
		{
			var ragdollClone = zombrag.Clone( this.GameObject.WorldPosition );

			var thisDresser = GameObject.GetComponent<Dresser>();
			var ragDresser = ragdollClone.GetComponent<Dresser>( true );

			if ( thisDresser != null && ragDresser != null )
			{
				ragDresser.Clothing = thisDresser.Clothing;
				ragDresser.WorkshopItems = thisDresser.WorkshopItems;
				ragDresser.Apply();

				thisDresser.Clear();
				thisDresser.Enabled = false;
			}

			var ragdollModel = ragdollClone.GetComponentInChildren<SkinnedModelRenderer>();
			if ( ragdollModel != null )
			{
				ragdollModel.SetBodyGroup( "Dismemberment", 1 );
			}

			var rago = ragdollClone.GetComponentInChildren<ModelPhysics>();

			if ( zombieModel != null ) zombieModel.UseAnimGraph = false;
			if ( MasterModel != null ) MasterModel.UseAnimGraph = false;
			
			if ( rago != null && zombieModel != null )
			{
				rago.CopyBonesFrom( zombieModel, true );
				rago.MotionEnabled = true;
			}
		}

		if ( zombieModel != null ) zombieModel.GameObject.Enabled = false;
		if ( MasterModel != null ) MasterModel.GameObject.Enabled = false;

		DestroyZombieMS();

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
	private void DestroyZombieMS()
	{
		GameObject.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 10f;

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
	private void RagdollIt( Vector3 hitPos, Vector3 impulseDir, string hitBoneName, float WeaponPower, GameObject hitObject )
	{
		ZombieCollider.Enabled = false;
		ZombieCollider2.Enabled = false;

		foreach ( var hitbox in GameObject.GetComponentsInChildren<ManualHitbox>() )
		{
			hitbox.GameObject.Enabled = false;
		}

		var zombrag = GameObject.GetPrefab( zombieRagdollPrefab.ResourcePath );
		var ragdollClone = zombrag.Clone( this.GameObject.WorldPosition );

		var gibMap = new Dictionary<string, int>
		{
			{ "head", 1 },
			{ "larm", 5 },
			{ "rarm", 4 },
			{ "lleg", 8 },
			{ "rleg", 9 },
			{ "rhand", 2 },
			{ "lhand", 3 },
			{ "rfoot", 6 },
			{ "lfoot", 7 },

		};

		var thisDresser = GameObject.GetComponent<Dresser>();
		var ragDresser = ragdollClone.GetComponent<Dresser>( true );

		ragDresser.Clothing = thisDresser.Clothing;
		ragDresser.WorkshopItems = thisDresser.WorkshopItems;
		ragDresser.Apply();

		thisDresser.Clear();
		thisDresser.Enabled = false;

		foreach ( var kvp in gibMap )
		{
			if ( hitObject.Tags.Has( kvp.Key ) )
			{


				var ragdollModel = ragdollClone.GetComponentInChildren<SkinnedModelRenderer>();
				//ragdollModel.body
				ragdollModel.SetBodyGroup( "Dismemberment",  kvp.Value);
				if ( hitObject.Tags.Contains( "head" ) )
				{
					foreach ( var x in ragDresser.Clothing )
					{
						switch ( x.Clothing.Category )
						{
							case Clothing.ClothingCategory.Hat:
								ragDresser.Clothing.Remove( x );
								ragDresser.Apply();
								break;
							case Clothing.ClothingCategory.GlassesEye:
								ragDresser.Clothing.Remove( x );
								ragDresser.Apply();
								break;
							case Clothing.ClothingCategory.GlassesSun:
								ragDresser.Clothing.Remove( x );
								ragDresser.Apply();
								break;
							case Clothing.ClothingCategory.GlassesSpecial:
								ragDresser.Clothing.Remove( x );
								ragDresser.Apply();
								break;

						}
					}			
				
				}
				if ( hitObject.Tags.Contains( "hand_L" ) || hitObject.Tags.Contains( "hand_R" ) )
				{
					foreach ( var x in ragDresser.Clothing )
					{
						switch ( x.Clothing.Category )
						{
							case Clothing.ClothingCategory.Gloves:
								ragDresser.Clothing.Remove( x );
								ragDresser.Apply();
								break;
							case Clothing.ClothingCategory.Wristwear:
								ragDresser.Clothing.Remove( x );
								ragDresser.Apply();
								break;

						}
					}
					
				}

				if ( !hitObject.Tags.Contains( "head" ) )
				{
					var c = Gibbin.FirstOrDefault( gib => gib.Key == kvp.Key ).Value;

					var gibbinclone = c.Clone(c.WorldTransform);
					//var gibber = gibbinclone.NetworkSpawn();
					gibbinclone.AddComponent<TemporaryEffect>().DestroyAfterSeconds = 10f;
					var z = gibbinclone.GetComponents<ModelPhysics>();
					
					foreach ( var m in z )
					{
						m.CopyBonesFrom( zombieModel, false );
						
						m.Bodies.ForEach( body => body.Component.ApplyImpulseAt( hitPos, impulseDir * ( WeaponPower / 4.5f ) ) );
					}
					break;
				}
				




			}
		}

		
		

		
		var rago = ragdollClone.GetComponentInChildren<ModelPhysics>();

		zombieModel.UseAnimGraph = false;
		MasterModel.UseAnimGraph = false;
		rago.CopyBonesFrom( zombieModel, true );

		//await Task.Delay(50);

		zombieModel.GameObject.Enabled = false;
		MasterModel.GameObject.Enabled = false;
		
		//zombieModel.Enabled = false;
		//zombieModel.Destroy();
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


			if ( !string.IsNullOrEmpty( hitBoneName ) && body.Component.GameObject.Name == hitBoneName )
			{

				var force = impulseDir * 6500;
				body.Component.ApplyImpulseAt( hitPos, force );
				impulseApplied = true;
				break;
			}
		}

		if ( !impulseApplied && rago.Bodies.Count > 0 )
		{

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
		zombieAttackHandle.StartSound();
	}



	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
		
		GameModeManager = Scene.GetAllComponents<GameModeManager>().FirstOrDefault();
	}


	protected override void OnUpdate()
	{
		//SyncAnimations();

		/*if ( TimeSinceLastMoan >= TimeTillNextMoan && isAlive )
		{
			//PlayMoanSound();
			TimeTillNextMoan = Rand.Int( 10, 18 );
			TimeSinceLastMoan = 0;
		}*/
	}
}
