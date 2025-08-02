using Sandbox;
using Sandbox.Services;
using System;

public sealed class MysteryBox : Component, IInteraction
{

	[Sync, Property]
	public float Cost { get; set; }

	[Sync]
	public WeaponManager WeaponManager { get; set; }

	private Random Random = new Random();

	TimeSince TimeSinceOpened;
	TimeSince TimeSinceChange;

	TimeSince TimeToTake;

	private WeaponData AWeapon;
	private WeaponData GivenWeapon;
	private WeaponData CurrentWeapon;

	private bool ChooseWeapon = false;

	private GameObject WeaponContainer { get; set; }

	[Property]
	private GameObject BoxLid { get; set; }

	float LidPitch = 0f;

	[Property]
	private GameObject WeaponStartPoint { get; set; }

	[Property]
	private GameObject WeaponEndPoint { get; set; }

	[Sync]
	private bool CanTakeWeapon { get; set; }



	protected override void OnUpdate()
	{
		//add closing after x seconds


		if ( ChooseWeapon )
		{
			OpenBoxLid();
			GoThroughWeapons();
		}
	
		if ( !ChooseWeapon && LidPitch != 0)
		{
			CloseBoxLid();
		}
	}




	protected override void OnStart()
	{
		WeaponManager = Scene.Get<WeaponManager>();
		if ( WeaponManager == null )
		{
			Log.Info( "No WeaponManager found!" );
		}	
	}


	[Rpc.Broadcast]
	private void OpenBoxLid()
	{


		float TargetPitch = -110f;
		float Speed = 3f;

		LidPitch = LidPitch.LerpTo( TargetPitch, Time.Delta * Speed );

		BoxLid.LocalRotation = Rotation.FromAxis( Vector3.Right, LidPitch );


		WeaponContainer.LocalPosition = WeaponContainer.LocalPosition.LerpTo(WeaponEndPoint.WorldPosition, Time.Delta * .75f);


	}


	[Rpc.Broadcast]
	private void CloseBoxLid()
	{
		Log.Info( "Close lid" );
		float TargetPitch = 0f;
		float Speed = 4f;

		LidPitch = LidPitch.LerpTo( TargetPitch, Time.Delta * Speed );

		BoxLid.LocalRotation = Rotation.FromAxis( Vector3.Right, LidPitch );
	}

	[Rpc.Broadcast]
	private void GoThroughWeapons()
	{
		if ( WeaponManager == null ) return;

		

		if ( TimeSinceChange >= .5f && TimeSinceOpened <= 8f)
		{
			AWeapon = WeaponManager.Weapons[Random.Next( WeaponManager.Weapons.Count() )];
			if(CurrentWeapon.Name == AWeapon.Name)
			{
				return;
			}
			CurrentWeapon = AWeapon;

			var weap = GameObject.GetPrefab( AWeapon.PrefabPath );
			
			var weapon = WeaponContainer.GetComponent<ModelRenderer>();
			if ( weapon != null )
			{
				weapon.Model = weap.GetComponentInChildren<SkinnedModelRenderer>().Model;
			}
			else
			{
				weapon = WeaponContainer.AddComponent<ModelRenderer>();
				weapon.Model = weap.GetComponentInChildren<SkinnedModelRenderer>().Model;
			}
			
			TimeSinceChange = 0f;
		}


		if ( TimeSinceOpened >= 8f )
		{
			GivenWeapon = AWeapon;
			CanTakeWeapon = true;
		}


	}

	[Rpc.Host]
	private void OpenBox()
	{
		Log.Info( "open box" );
		WeaponContainer = new GameObject();
		WeaponContainer.WorldPosition = WeaponStartPoint.WorldPosition;
		WeaponContainer.WorldRotation = Rotation.FromYaw( 90 );
		OpenBoxLid();
		TimeSinceOpened = 0f;
		TimeSinceChange = 0f;
		ChooseWeapon = true;
		
	}


	[Rpc.Broadcast]
	private void PlayerTakeWeapon(Player player)
	{
		WeaponManager.GivePlayerStartingWeapon( GivenWeapon.Name, player );
		WeaponContainer?.Destroy();
		
	}


	[Rpc.Host]
	private void CloseBox()
	{
		CanTakeWeapon = false;
		ChooseWeapon = false;
	}




	public void OnInteract( Player player )
	{

		//add reject interaction


		if ( CanTakeWeapon )
		{
			Log.Info( "take weapon" );
			PlayerTakeWeapon( player );
			CloseBox();
			return;
		}

		if ( ChooseWeapon ) return;

		Log.Info( "open boxa" );
		OpenBox();

		
	}




}
