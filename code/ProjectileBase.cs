using Sandbox;
using System;

public abstract class ProjectileBase : Component
{
	[Sync] protected Vector3 Velocity { get; set; }
	[Sync] protected GameObject Attacker { get; set; }
	[Sync] protected float Damage { get; set; }
	[Sync] protected float ExplosionRadius { get; set; }
	[Sync] protected float ExplosionImpulse { get; set; }

	[Property] protected float Gravity { get; set; } = 800f;
	[Property] protected float MaxLifetime { get; set; } = 8f;
	[Property] protected float MinDetonationSpeed { get; set; } = 30f;

	protected bool Exploded { get; private set; }

	private TimeSince TimeSinceSpawned;

	public void Configure( GameObject attacker, Vector3 initialVelocity, float damage, float impulse, float radius )
	{
		Attacker = attacker;
		Velocity = initialVelocity;
		Damage = damage;
		ExplosionImpulse = impulse;
		ExplosionRadius = radius;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Networking.IsHost || Exploded )
		{
			return;
		}

		SimulateMovement( Time.Delta );

		if ( TimeSinceSpawned >= MaxLifetime )
		{
			Explode( GameObject.WorldPosition, Vector3.Up );
		}
	}

	private void SimulateMovement( float deltaTime )
	{
		var start = GameObject.WorldPosition;
		Velocity += Vector3.Down * Gravity * deltaTime;
		var end = start + Velocity * deltaTime;

		var trace = Scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "capsule" )
			.WithoutTags( "capplay" )
			.UseHitboxes( true )
			.Run();

		if ( trace.Hit )
		{
			HandleImpact( trace );
			return;
		}

		GameObject.WorldPosition = end;
		if ( Velocity.LengthSquared > 1f )
		{
			GameObject.WorldRotation = Rotation.LookAt( Velocity.Normal, Vector3.Up );
		}
	}

	protected virtual void HandleImpact( SceneTraceResult hit )
	{
		if ( ShouldExplodeOnImpact( hit ) && Velocity.Length >= MinDetonationSpeed )
		{
			Explode( hit.HitPosition, hit.Normal );
			return;
		}

		GameObject.WorldPosition = hit.HitPosition;
		Velocity = Vector3.Zero;
	}

	protected virtual bool ShouldExplodeOnImpact( SceneTraceResult hit ) => true;

	protected void Explode( Vector3 position, Vector3 normal )
	{
		if ( Exploded || !Networking.IsHost )
		{
			return;
		}

		Exploded = true;
		ApplyExplosionDamage( position );
		OnExploded( position, normal );
		GameObject.Destroy();
	}

	protected virtual void OnExploded( Vector3 position, Vector3 normal )
	{
	}

	private void ApplyExplosionDamage( Vector3 center )
	{
		float radius = Math.Max( ExplosionRadius, 1f );

		foreach ( var zombie in Scene.GetAllComponents<Zombie>() )
		{
			if ( zombie == null || !zombie.isAlive )
			{
				continue;
			}

			var zombiePos = zombie.GameObject.WorldPosition;
			float distance = zombiePos.Distance( center );
			if ( distance > radius )
			{
				continue;
			}

			float falloff = 1f - (distance / radius);
			float scaledDamage = Math.Max( 1f, Damage * Math.Max( 0.2f, falloff ) );
			var dir = zombiePos - center;
			dir = dir.LengthSquared < 0.001f ? Vector3.Up : dir.Normal;

			var damageInfo = new DamageInfo
			{
				Attacker = Attacker,
				Damage = scaledDamage
			};

			
		}
	}
}
