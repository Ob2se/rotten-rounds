using Sandbox;

public enum PowerupID
{
	None = 0,
	MaxAmmo = 1,
	InstantKill = 2,
	DoublePoints = 3,
	FireSale = 4,
	Nuke = 5,
}

public static class PowerUpDatabase
{
	[Property]
	public static PowerupID PowerUpToDrop { get; set; }

	public static PowerUpClass GetData( PowerupID type )
	{
		// Construct fresh objects every time
		return type switch
		{
			PowerupID.MaxAmmo => new PowerUpClass
			{
				PowerUpName = "Max Ammo",
				PowerUpModelPath = "models/powerups/maxammo/maxammo2.vmdl"
			},

			PowerupID.InstantKill => new PowerUpClass
			{
				PowerUpName = "Insta Kill",
				PowerUpModelPath = "models/powerups/instakill/instakill2.vmdl"
			},

			PowerupID.DoublePoints => new PowerUpClass
			{
				PowerUpName = "Double Points",
				PowerUpModelPath = "models/powerups/2x/x2.vmdl"
			},

			PowerupID.FireSale => new PowerUpClass
			{
				PowerUpName = "Fire Sale",
				PowerUpModelPath = "models/powerups/firesale/firesale2.vmdl"
			},

			PowerupID.Nuke => new PowerUpClass
			{
				PowerUpName = "Nuke",
				PowerUpModelPath = "models/powerups/nuke/nuke2.vmdl"
			},

			_ => default // returns null for PowerupID.None
		};
	}
}
