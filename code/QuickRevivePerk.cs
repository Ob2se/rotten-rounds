using Sandbox;

public sealed class QuickRevivePerk : Component
{

	[Property]
	public Player targetPlayer { get; set; }


	protected override void OnStart()
	{
		base.OnStart();
		if ( targetPlayer != null )
		{
			targetPlayer.HasQuickRevive = true;
			targetPlayer.PerkIcons.Add(PerkDatabase.GetData(PerkID.QuickRevive).PerkIcon);
			Log.Info( PerkDatabase.GetData( PerkID.QuickRevive ).PerkIcon );
			Log.Info( "ICON EXISTS => " + FileSystem.Mounted.FileExists( PerkDatabase.GetData( PerkID.QuickRevive ).PerkIcon ) );
			Log.Info("Quick Revive Perk Applied to " + targetPlayer.PlayerConnection.DisplayName);
		}
	}

	protected override void OnUpdate()
	{

	}
}
