using Sandbox;
using System;

public sealed class GrenadeProjectile : ProjectileBase
{
	[Property] public float FuseTime { get; set; } = 2.75f;
	[Property] public float BounceRestitution { get; set; } = 0.55f;
	[Property] public float SurfaceFriction { get; set; } = 0.75f;
	[Property] public float MinBounceSpeed { get; set; } = 120f;

	[Sync] private string ExplosionEffectPath { get; set; } = string.Empty;
	[Sync] private string ExplosionSoundPath { get; set; } = string.Empty;
	[Sync] private bool IsLethal { get; set; } = true;

	private TimeSince TimeSinceThrown;

	public void SetFuseTime( float seconds )
	{
		FuseTime = Math.Max( 0.1f, seconds );
	}

	public void SetExplosionData( string effectPath, string soundPath, bool lethal )
	{
		ExplosionEffectPath = effectPath ?? string.Empty;
		ExplosionSoundPath = soundPath ?? string.Empty;
		IsLethal = lethal;
	}

	protected override void OnStart()
	{
		base.OnStart();
		Gravity = 1100f;
		MaxLifetime = Math.Max( MaxLifetime, FuseTime + 2f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy || !Networking.IsHost || Exploded )
		{
			return;
		}

		if ( TimeSinceThrown >= FuseTime )
		{
			Explode( GameObject.WorldPosition, Vector3.Up );
		}
	}

	protected override bool ShouldExplodeOnImpact( SceneTraceResult hit )
	{
		return hit.Tags.Contains( "zombie" );
	}

	protected override void HandleImpact( SceneTraceResult hit )
	{
		if ( ShouldExplodeOnImpact( hit ) && Velocity.Length >= MinDetonationSpeed )
		{
			Explode( hit.HitPosition, hit.Normal );
			return;
		}

		float incomingSpeed = Velocity.Length;
		var reflected = Velocity - (2f * Vector3.Dot( Velocity, hit.Normal ) * hit.Normal);

		var tangent = reflected - (hit.Normal * Vector3.Dot( reflected, hit.Normal ));
		reflected -= tangent * (1f - SurfaceFriction);
		reflected *= BounceRestitution;

		GameObject.WorldPosition = hit.HitPosition + hit.Normal * 1.5f;
		Velocity = reflected;

		if ( incomingSpeed < MinBounceSpeed )
		{
			Velocity *= 0.15f;
			if ( Velocity.Length < 20f )
			{
				Velocity = Vector3.Zero;
			}
		}
	}

	protected override void OnExploded( Vector3 position, Vector3 normal )
	{
		PlayExplosionEffects( position, ExplosionEffectPath, ExplosionSoundPath );
	}

	[Rpc.Broadcast]
	private void PlayExplosionEffects( Vector3 position, string effectPath, string soundPath )
	{
		if ( !string.IsNullOrWhiteSpace( effectPath ) )
		{
			var prefab = GameObject.GetPrefab( effectPath );
			prefab?.Clone( position );
		}

		if ( !string.IsNullOrWhiteSpace( soundPath ) )
		{
			Sound.Play( soundPath, position );
		}
	}
}
