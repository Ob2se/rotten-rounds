using Sandbox;
using System.Numerics;

public sealed class WallBuy : Component, IInteraction
{

	[Property]
	public string WeaponName { get; set; }

	[Property]
	public int Cost { get; set; }

	WeaponManager WeaponManager { get; set; }

	protected override void OnUpdate()
	{

	}


	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			WeaponManager = Scene.GetAll<WeaponManager>().FirstOrDefault();
		}
		
	}


	private void GiveWeapon( Player player )
	{
		Log.Info( "gave weapon?" );
		WeaponManager.GivePlayerStartingWeapon( WeaponName, player );
	}


	private bool PriceCheck(Player player)
	{
		
		return true;
	}

	[Rpc.Host]
	private void BuyWeapon( Player player )
	{
		if ( Networking.IsHost )
		{
			if ( player.Points - Cost < 0 )
			{
				Log.Info( "broke" );
				return;
			}
			player.RemovePoints( Cost );
			GiveWeapon( player );
		}
	}


	public void OnInteract( Player player )
	{
		BuyWeapon( player );
	}



}
