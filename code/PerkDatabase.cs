using Sandbox;

public enum PerkID
{
	None = 0,
	IcyAid = 1,
	JitterJuice = 2,
	BurleyBrew = 3,
	QuickSip = 4,
}

public static class PerkDatabase
{
	private static Dictionary<PerkID, PerkInfo> _data;

	// Lazy-initialize to avoid static initializer issues with type resolution
	public static Dictionary<PerkID, PerkInfo> Data
	{
		get
		{
			if ( _data == null )
			{
				_data = new Dictionary<PerkID, PerkInfo>
				{
					{ PerkID.IcyAid, new PerkInfo() { PerkName = "Icy Aid", PerkIcon = "/ui/perks/icyaid/icylogomain.png", PerkMachineTexture = "models/perks/machine/material/icyaid.vmat", PerkCost = 750, PerkComp = typeof(QuickRevivePerk) } },
					{ PerkID.JitterJuice, new PerkInfo() { PerkName = "Jitter Juice", PerkIcon = "/ui/perks/icyaid/jjph.png", PerkMachineTexture = "models/perks/machine/material/jitterjuiceph.vmat", PerkCost = 2000, PerkComp = typeof(JitterJuice) } },
					{ PerkID.BurleyBrew, new PerkInfo() { PerkName = "Burley Brew", PerkIcon = "/ui/perks/icyaid/bbph.png", PerkMachineTexture = "models/perks/machine/material/burleybrewph.vmat", PerkCost = 2500, PerkComp = typeof(BurleyBrew) } },
					{ PerkID.QuickSip, new PerkInfo() { PerkName = "Quick Sip", PerkIcon = "/ui/perks/icyaid/quicksip.png", PerkMachineTexture = "models/perks/machine/material/quicksip.vmat", PerkCost = 3000, PerkComp = typeof(QuickSip) } },
				};
			}
			return _data;
		}
	}

	public static PerkInfo GetData( PerkID type )
	{
		if ( Data.TryGetValue( type, out var info ) )
		{
			return info;
		}

		Log.Error( $"PerkDatabase: No data found for PerkID '{type}'" );
		return null;
	}
}
