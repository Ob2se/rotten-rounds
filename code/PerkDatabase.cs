using Sandbox;

public enum PerkID
{
	None = 0,
	QuickRevive = 1,
}

public static class PerkDatabase
{
	[Property]
	public static PerkID PerkForMachine { get; set; }

	// Map enum values to structs
	public static readonly Dictionary<PerkID, PerkInfo> Data = new()
	{
		{ PerkID.QuickRevive, new PerkInfo(){PerkName = "Quick Revive", PerkIcon = "/ui/perks/icyaid/icylogomain.png", PerkMachineTexture =  "models/perks/machine/material/icyaid.vmat",PerkCost = 500, PerkCompName = "QuickRevivePerk" } },
	};

	public static PerkInfo GetData( PerkID type )
	{
		return Data[type];
	}
}
