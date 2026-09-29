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

	[Property, Group( "Weapon Art" )] public SoundFile ShotSound { get; set; }

	[Property, Group("Weapon Art")] public SoundFile ReloadSound { get; set; }

	[Property, Group( "Weapon Setup" ), HideIf("HideAutomatic", true)] public bool Automatic { get; set; }

	[Property, Group( "Weapon Setup" )] public string WeaponName { get; set; }

	[Property, Group("Weapon Setup"), HideIf( "HideBolt", true )] public bool BoltAction { get; set; }

	[Property, Group( "Weapon Setup" ), HideIf( "HidePump", true )] public bool PumpAction { get; set; }

	[Property, Group( "Pack a Punch Settings" )] public string PackedName { get; set; }


	[Property, Group( "Weapon Stats" ), MinMax( 1, 100000 )] public float WeaponPower { get; set; } = 1f;


	[Property, Group( "Weapon Stats" ), MinMax( 60, 100000 )]
	public float FireRate { get; set; } = 60f;

	[Property, Group( "Weapon Stats" ), MinMax( 1, 100000 )]
	public int MagMax { get; set; } = 1;

	[Property, Group( "Weapon Stats" ), MinMax( 1, 100000 )]
	public int AmmoMax { get; set; } = 1;


	[Property, Group( "Weapon Stats" ), MinMax( 1, 100000 )]
	public int WeaponDamage { get; set; } = 1;

	[Property, Group( "Weapon Stats" ), HideIf( "PumpAction", false )]
	public float PumpTime { get; set; } = .75f;

	[Property, Group( "Weapon Stats" ), HideIf( "BoltAction", false )]
	public float BoltTime { get; set; } = .75f;


	[Property, Group("Weapon Stats")]
	public int ReloadTime { get; set; }

	[Property, Group( "Pack a Punch Settings" ), ShowIf("WeaponType", weaponType.Launcher)]
	public bool DontExpandMagSize { get; set; }


	[Property, Group( "Weapon Setup" )]
	public Vector3 WeaponOffsetPos { get; set; }

	[Property, Group( "Weapon Setup" )]
	public Vector3 WeaponOffsetRot { get; set; }

	[Property]
	public SkinnedModelRenderer fpsArms { get; set; }


	public bool HideBolt => WeaponType != weaponType.Sniper;

	public bool HidePump => WeaponType != weaponType.Shotgun;

	public bool HideAutomatic => WeaponType == weaponType.Sniper || WeaponType == weaponType.Launcher;

	public Material WeaponMaterial { get; set; }


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

	protected override void OnStart()
	{
		base.OnStart();

		if ( WeaponMaterial != null )
		{
			WeaponModel.MaterialOverride = WeaponMaterial;
		}
		
	}
}
