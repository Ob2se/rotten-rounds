using Sandbox;

public interface IInventoryInterface
{
	public void AddWeapon( string WeaponPrefab );

	public void RemoveWeapon( int slot );

	public void GiveAmmo( int slot, int amount, int magMax );




}
