using Sandbox;
using System;
using static Sandbox.Citizen.CitizenAnimationHelper;

public sealed class PowerUp : Component
{
	[Property]
	public ModelRenderer PowerUpModel { get; set; }

	[Property]
	public GameObject PowerUpVFX { get; set; }
	
	public PowerupID PowerUpType { get; set; }

	[Property]
	public BoxCollider PowerUpTriggerBox { get; set; }

	bool blinking = false;
	int blinkIndex = 0;                 // counts how many blinks have happened
	float nextBlinkInterval = 0f;       // interval until next blink
	TimeSince TimeSincePowerUpDropped;  // auto-incrementing
	TimeSince timeSinceBlink;           // auto-incrementing

	float blinkStartTime = 20f;
	float blinkDuration = 10f;          // total time for acceleration
	float maxInterval = .5f;
	float minInterval = 0.1f;
	int totalBlinks = 50;               // approximate number of blinks over duration

	protected override void OnStart()
	{
		base.OnStart();
		TimeSincePowerUpDropped = 0f;
		if ( Networking.IsHost )
		{
			
			var rand = new Random();
			var vals = Enum.GetValues( typeof( PowerupID ) );
			var index = rand.Next( 1, vals.Length );
			
			var type = (PowerupID)vals.GetValue( index );
			SetPowerup( (int)type );
			
		}
		PowerUpTriggerBox.OnObjectTriggerEnter += OnTriggerEnter;
	}


	[Rpc.Broadcast]
	private void SetPowerup( int vals ) 
	{
		
		PowerUpType = (PowerupID)vals;
		var model = Model.Load( PowerUpDatabase.GetData( PowerUpType ).PowerUpModelPath );

		PowerUpModel.Model = model;

		PowerUpModel.Enabled = true;



		PowerUpTriggerBox.Center = PowerUpModel.Model.Bounds.Center;
		PowerUpTriggerBox.Scale = PowerUpModel.Model.Bounds.Size;

	}


	private void OnTriggerEnter( GameObject other )
	{
		
		var player  = other.GetComponent<Player>();
		if ( player == null ) return;


		if ( player != null )
		{
			ActivatePowerUp();
			DestroyPowerup();
		}
		
	}

	[Rpc.Host]
	private void ActivatePowerUp()
	{
		var gamemodemanager = Scene.GetAllComponents<GameModeManager>().FirstOrDefault();
		if ( gamemodemanager != null )
		{
			var powerupname = PowerUpDatabase.GetData( PowerUpType ).PowerUpName;

			switch ( powerupname )
			{
				case "Max Ammo":
					PlayPowerupSound( "sound/powerups/maxammo.sound" );
					gamemodemanager.MaxAmmo();
					break;
				case "Insta Kill":
					PlayPowerupSound( "sound/powerups/instantkill.sound" );
					gamemodemanager.InstaKillStart();
					break;
				case "Double Points":
					PlayPowerupSound( "sound/powerups/doublepoints.sound" );
					gamemodemanager.DoublePointsStart();
					break;
				case "Nuke":
					PlayPowerupSound( "sound/powerups/kaboom.sound" );
					gamemodemanager.KillAllZombies();
					break;
				case "Fire Sale":
					PlayPowerupSound( "sound/powerups/firesale.sound" );
					gamemodemanager.StartFireSale();
					break;
			}
		}
	}

	[Rpc.Broadcast]
	private void PlayPowerupSound(string soundfile)
	{

		//Sound.
		Sound.Play( soundfile );
	}


	[Rpc.Broadcast]
	private void DestroyPowerup()
	{
		GameObject?.Destroy();
	}


	private void Blink()
	{
		
		if ( PowerUpModel.Enabled )
		{
			PowerUpModel.Enabled = false;
			
			
			return;
		}
		if ( !PowerUpModel.Enabled )
		{
			
			PowerUpModel.Enabled = true;
			
			return;
		}
	}


	protected override void OnUpdate()
	{

		PowerUpModel.WorldRotation *= Rotation.FromYaw( 90f * Time.Delta );

		if ( !blinking )
		{
			if ( TimeSincePowerUpDropped >= blinkStartTime )
			{
				blinking = true;
				timeSinceBlink = 0f;
				blinkIndex = 0;
				nextBlinkInterval = maxInterval;
			}
			return;
		}

		if ( timeSinceBlink >= nextBlinkInterval )
		{
			timeSinceBlink = 0f;
			Blink();  // <-- your blink effect

			blinkIndex++;

			
			float u = MathX.Clamp( (float)blinkIndex / totalBlinks, 0f, 1f );

			// cubic easing for smooth acceleration
			float eased = 1f - MathF.Pow( 1f - u, 3f );

			// schedule next blink interval
			nextBlinkInterval = MathX.Lerp( maxInterval, minInterval, eased );
		}




	}
}
