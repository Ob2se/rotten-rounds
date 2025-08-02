using Sandbox;
using System.Numerics;

public interface IWeapon
{
	void OnShoot();

	void LineTrace();

	void HandleTrace();
}
