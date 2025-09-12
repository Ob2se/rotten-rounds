using Sandbox;
using Sandbox.Citizen;
using System;
using System.Numerics;
using System.Threading.Tasks;
using static Sandbox.Package;
using static Sandbox.VertexLayout;

//namespace rrcustomcontentlibrary;



public class BaseWeapon : Component
{
	[Property] public SkinnedModelRenderer WeaponModel { get; set; }

	[Property] public PrefabFile MuzzleFlashPrefab { get; set; }

	[Property] public PrefabFile ImpactEffectPrefab { get; set; }

	[Property] public PrefabFile FleshImpactEffectPrefab { get; set; }

	[Property] public bool Automatic { get; set; }

	[Property] public string WeaponName { get; set; }


	

	[Property]
	public float FireRate { get; set; }

	[Property]
	public int MagMax { get; set; }

	[Property]
	public int AmmoMax { get; set; }

	[Property]
	public int WeaponRange { get; set; }

	[Property]
	public int WeaponDamage { get; set; }


	[Property]
	public int ReloadTime { get; set; }



	public enum weaponType
	{
		Pistol = 0,
		Smg = 1,
		Rifle = 2,
		Shotgun = 3,
		Lmg = 4,
		Special = 5
	}

	[Property]
	public weaponType WeaponType { get; set; }


}
