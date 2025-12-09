using Sandbox;
using Sandbox.Citizen;
using System;
using System.Numerics;
using System.Threading.Tasks;
using static Sandbox.Package;
using static Sandbox.VertexLayout;




[Title( "Base Weapon" )]
[Category( "RRCustomContent" )]
public partial class BaseWeapon : Component
{
	[Property, Group( "Weapon Art" )] public SkinnedModelRenderer WeaponModel { get; set; }

	[Property, Group( "Weapon Art" )] public PrefabFile MuzzleFlashPrefab { get; set; }

	[Property, Group( "Weapon Art" )] public PrefabFile ImpactEffectPrefab { get; set; }

	[Property, Group( "Weapon Art" )] public PrefabFile FleshImpactEffectPrefab { get; set; }

	[Property, Group( "Weapon Setup" ), HideIf("HideAutomatic", true)] public bool Automatic { get; set; }

	[Property, Group( "Weapon Setup" )] public string WeaponName { get; set; }

	[Property, Group( "Pack a Punch Settings" )] public string PackedName { get; set; }


	[Property, Group( "Weapon Stats" )] public float WeaponPower { get; set; }


	[Property, Group( "Weapon Stats" )]
	public float FireRate { get; set; }

	[Property, Group( "Weapon Stats" )]
	public int MagMax { get; set; }

	[Property, Group( "Weapon Stats" )]
	public int AmmoMax { get; set; }


	[Property, Group( "Weapon Stats" )]
	public int WeaponDamage { get; set; }


	[Property, Group("Weapon Stats")]
	public int ReloadTime { get; set; }

	[Property, Group( "Pack a Punch Settings" ), ShowIf("WeaponType", weaponType.Launcher)]
	public bool DontExpandMagSize { get; set; }


	public bool HideAutomatic => WeaponType == weaponType.Sniper || WeaponType == weaponType.Launcher;

	public enum weaponType
	{
		Pistol = 0,
		Smg = 1,
		Rifle = 2,
		Shotgun = 3,
		Sniper = 4,
		Launcher = 5,
		Special = 6
	}

	[Property, Group( "Weapon Setup" )]
	public weaponType WeaponType { get; set; }


}
