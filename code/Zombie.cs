using Sandbox;
using System;
using System.Dynamic;
using System.Numerics;

public sealed class Zombie : Component
{



	[Sync, Property] public SimpleZombieController SimpleZombieController { get; set; }

	[Sync, Property, Change("TookDamage")] float Health { get; set; }

	[Property]
	private GameModeManager GameModeManaga { get; set; }	

	[Property]
	SkinnedModelRenderer zombieModel { get; set; }

	[Property]
	Collider ZombieCollider { get; set; }

	public bool isAlive = true;

	public static event Action ZombieDied;


	[Rpc.Host]
	public void TakeDamage( DamageInfo DamageInfo )
	{
		if ( SimpleZombieController.CurrentState != SimpleZombieController.ZomState.Dead )
		{ 
			RemoveHealth( DamageInfo.Damage );
			if ( Health <= 0 )
			{
				GivePlayerPoints( DamageInfo.Attacker, 50 );
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
			ZombieDead();
			ControlZombieController();
			
		}

	}


	[Rpc.Host]
	public void ZombieDead()
	{
		isAlive = false;
		GameModeManaga.ZombieDeath();
		ZombieRagdoll();
		DestroyZombieMS( 6500 );
		
	}


	[Rpc.Broadcast]
	private async void DestroyZombieMS(int ms)
	{
		await Task.Delay( ms );
		GameObject.Destroy();
	}


	[Rpc.Broadcast]
	public void ZombieRagdoll()
	{
		ZombieCollider.Enabled = false;
		var rag = GameObject.AddComponent<ModelPhysics>();
		rag.Model = zombieModel.Model;
		rag.Renderer = zombieModel;
		rag.CopyBonesFrom( zombieModel ,false);
		rag.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;
		DisableRagdoll(rag);
	}


	private async void DisableRagdoll(ModelPhysics ragdoll)
	{
		await Task.Delay( 2000 );
		ragdoll.PhysicsGroup.Sleeping = true;
		foreach ( var x in ragdoll.PhysicsGroup.Bodies )
		{
			x.EnableSolidCollisions = false;
		}
	}


	[Rpc.Broadcast]
	private void ControlZombieController()
	{
		SimpleZombieController.ChangeState( SimpleZombieController.ZomState.Dead);
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;
		GameModeManaga = Scene.Directory.FindByName( "GameModeManager" ).First().GetComponent<GameModeManager>();
	}


	protected override void OnUpdate()
	{

	}
}
