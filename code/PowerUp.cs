using Sandbox;
using System;
using static Sandbox.Citizen.CitizenAnimationHelper;

public sealed class PowerUp : Component
{
	[Property]
	public ModelRenderer PowerUpModel { get; set; }

	
	public PowerupID PowerUpType { get; set; }

	[Property]
	public BoxCollider PowerUpTriggerBox { get; set; }


	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost )
		{
			
			var rand = new Random();
			var vals = Enum.GetValues( typeof( PowerupID ) );
			var index = rand.Next( 1, vals.Length );

			var type = (PowerupID)vals.GetValue( index );
			SetPowerup( (int)type );
			Log.Info( type );
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
		Log.Info( "hello triggerbox power activated" );
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
					Log.Info( "maxammo picked up" );
					gamemodemanager.MaxAmmo();
					break;
				case "Insta Kill":
					gamemodemanager.InstaKillStart();
					break;
				case "Double Points":
					gamemodemanager.DoublePointsStart();
					break;
				case "Nuke":
					gamemodemanager.KillAllZombies();
					break;
			}
		}
	}


	[Rpc.Broadcast]
	private void DestroyPowerup()
	{
		GameObject?.Destroy();
	}

	protected override void OnUpdate()
	{
		WorldRotation *= Rotation.FromYaw( 90f * Time.Delta );
	}
}
