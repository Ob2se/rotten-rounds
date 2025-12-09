using Sandbox;
using System;

public sealed class LightFlickerPlaySign : Component
{
	[Property] public PointLight Light;
	[Property] public Material FlickerMat;

	[Property] public float FlickerInterval = 8f;   // Max time between flicker bursts
	[Property] public float BurstDuration = 1.2f;   // How long the burst lasts
	[Property] public float MinFlickerTime = 0.03f; // Min blink speed
	[Property] public float MaxFlickerTime = 0.15f; // Max blink speed
	[Property] public float EmissiveStrength = 10f;
	[Property] public float LightStrength = 500f;

	private float timeSinceLastBurst = 0f;
	private float burstTimer = 0f;
	private float nextFlickTime = 0f;
	private float nextBurstDelay = 0f;
	private bool isFlickering = false;
	private bool lightOn = false;

	public bool isHovered {  get; set; }

	protected override void OnStart()
	{
		// choose initial random delay
		nextBurstDelay = Game.Random.Float( 1f, FlickerInterval );
	}

	protected override void OnUpdate()
	{

		if(isHovered)
		{
			Light.Enabled = true;
			FlickerMat.Set( "Flickerer", 10f );
			timeSinceLastBurst = 0f;
			return;

		}


		float dt = Time.Delta;
		timeSinceLastBurst += dt;

		// normally off
		if ( !isFlickering )
		{
			SetLight( false );
		}

		// start a flicker burst
		if ( !isFlickering && timeSinceLastBurst >= nextBurstDelay )
		{
			isFlickering = true;
			burstTimer = 0f;
			nextFlickTime = Game.Random.Float( MinFlickerTime, MaxFlickerTime );
			timeSinceLastBurst = 0f;

			// pick next random interval for future burst
			nextBurstDelay = Game.Random.Float( 1f, FlickerInterval );
		}

		if ( isFlickering )
		{
			burstTimer += dt;
			nextFlickTime -= dt;

			// toggle light randomly during burst
			if ( nextFlickTime <= 0f )
			{
				lightOn = !lightOn;
				SetLight( lightOn );
				nextFlickTime = Game.Random.Float( MinFlickerTime, MaxFlickerTime );
			}

			// end burst
			if ( burstTimer >= BurstDuration )
			{
				isFlickering = false;
				SetLight( false );
			}
		}
	}

	private void SetLight( bool on )
	{
		if ( Light != null )
		{
			Light.Enabled = !on;
		}

		if ( FlickerMat != null )
			FlickerMat.Set( "Flickerer", on ? EmissiveStrength : 0f );
	}



}
