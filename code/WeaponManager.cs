using Sandbox;
using System;
using static Sandbox.Clothing;
using System.Numerics;
using static Sandbox.PhysicsContact;

public sealed class WeaponManager : Component
{
	

	[Sync( SyncFlags.FromHost )]
	public List<string> WeaponPaths { get; set; } = new();
	

	public string StartingWeapon { get; set; }


	protected override void OnStart()
	{
		base.OnStart();

	}


	[Rpc.Host]
	public void GivePlayerWeapon(string Weapon, Player player, int slot)
	{
	
		if ( WeaponPaths.Contains(Weapon) )
		{
			player.Inventory.AddWeapon( Weapon, slot );
		}
		
	}


	protected override void OnUpdate()
	{

	}
}
