using Sandbox;
using System;
using System.Dynamic;
using System.Numerics;
using System.Threading.Tasks;

public sealed class Zombie : Component
{



	[Sync, Property] public SimpleZombieController SimpleZombieController { get; set; }

	[Sync, Property, Change("TookDamage")] float Health { get; set; }
	

	[Property]
	SkinnedModelRenderer zombieModel { get; set; }

	[Property]
	Collider ZombieCollider { get; set; }

	[Property]
	ModelPhysics rag { get; set; }

	[Property]
	Rigidbody ZombieRigidbody { get; set; }

	public bool isAlive = true;

	public static event Action ZombieDied;


	[Rpc.Host]
	public void TakeDamage( DamageInfo DamageInfo, Vector3 hitPos, int hitBone, Vector3 Direction )
	{
		if ( SimpleZombieController.CurrentState != SimpleZombieController.ZomState.Dead )
		{ 
			RemoveHealth( DamageInfo.Damage );
			//ZombieRigidbody.PhysicsBody.ApplyImpulseAt( hitPos, Direction * 20000 );
			if ( Health <= 0 )
			{
				GivePlayerPoints( DamageInfo.Attacker, 50 );
				//ZombieRigidbody.ApplyImpulseAt( hitPos, Direction * 200000 );
				ZombieDead();
				ZombieRagdoll(hitBone, hitPos, Direction);
				ControlZombieController();
				
			}
			else
			{
				GivePlayerPoints( DamageInfo.Attacker, 20 );
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
	public void ZombieRagdoll(int hitBone, Vector3 hitPos, Vector3 impulseDir)
	{
		ZombieCollider.Enabled = true;
		ZombieRigidbody.PhysicsBody.ApplyImpulseAt( hitPos, impulseDir * 200000 * 2 );
		ZombieRigidbody.MassOverride = 2000f;
		//ZombieRigidbody.Gravity = false;
		//ZombieRigidbody.PhysicsBody.GravityEnabled = false;
		//var rag = GameObject.AddComponent<ModelPhysics>(false);

		_ = RagdollIt( rag );
		

		GameObject.Tags.Remove( "zombie" );
		GameObject.Tags.Add( "zombieDead" );
		//_ = DisableRagdoll(rag);
	}

	private async Task RagdollIt(ModelPhysics rag)
	{
		await Task.Delay( 50 );
		rag.Enabled = true;
		rag.Model = zombieModel.Model;
		rag.Renderer = zombieModel;
		rag.CopyBonesFrom( zombieModel, false );
		rag.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;

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
		SimpleZombieController.ChangeState( SimpleZombieController.ZomState.Dead);
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
		//GameModeManaga = Scene.Directory.FindByName( "GameModeManager" ).First().GetComponent<GameModeManager>();
	}


	protected override void OnUpdate()
	{

	}
}
