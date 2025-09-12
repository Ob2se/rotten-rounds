using Sandbox;
using Sandbox.Services;
using Sandbox.UI;
using System;

public sealed class MysteryBox : Component, IInteraction
{

	[Sync, Property]
	public float Cost { get; set; }

	[Sync]
	public WeaponManager WeaponManager { get; set; }


	public Random Random { get; set; } = new Random();

	[Sync]
	public TimeSince TimeSinceOpened { get; set; }

	[Sync]
	public TimeSince TimeSinceChange { get; set; }

	[Sync]
	public TimeSince TimeSinceFinished { get; set; }

	float TimeToTake { get; } = 9f;

	[Sync]
	public string AWeapon { get; set; }
	[Sync]
	public string GivenWeapon { get; set; }
	[Sync]
	public string CurrentWeapon { get; set; }


	[Sync]
	public bool ChooseWeapon { get; set; } = false;

	[Sync]
	bool BoxOpen { get; set; } = false;

	[Sync]
	public GameObject WeaponContainer { get; set; }

	[Property]
	public GameObject BoxLid { get; set; }

	[Sync]
	public float LidPitch { get; set; } = 0f;

	[Property]
	public GameObject WeaponStartPoint { get; set; }

	[Property]
	public GameObject WeaponEndPoint { get; set; }

	[Sync]
	public bool CanTakeWeapon { get; set; }

	private bool BoxNeedsToClose { get; set; } = false;

	protected override void OnUpdate()
	{
		//add closing after x seconds
		//Log.Info( BoxOpen );

		if ( ChooseWeapon )
		{
			OpenBoxLid();
			GoThroughWeapons();
		}

		if ( !BoxOpen && BoxNeedsToClose )
		{
			CloseBoxLid();
			if ( LidPitch >= 0f )
			{
				BoxNeedsToClose = false;
			}
		}

		if ( TimeSinceFinished >= TimeToTake && CanTakeWeapon )
		{
			CloseBox();

			CanTakeWeapon = false;
			GivenWeapon = null;
			AWeapon = null;
			CurrentWeapon = null;
			WeaponContainer?.Destroy();
			Log.Info( "Closing box after time" );
		}


	}




	protected override void OnStart()
	{
		WeaponManager = Scene.GetAllComponents<WeaponManager>().FirstOrDefault();
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




		if ( WeaponContainer == null )
		{
			WeaponContainer = Scene.FindAllWithTag( "MBWeaponContainer" ).FirstOrDefault();
			Log.Info( WeaponContainer );
			return;
		}

		WeaponContainer.LocalPosition = WeaponContainer.LocalPosition.LerpTo( WeaponEndPoint.WorldPosition, Time.Delta * .75f );

	}

	[Rpc.Host]
	private void UpdateWeaponContainer()
	{

	}


	[Rpc.Broadcast]
	private void CloseBoxLid()
	{
		Log.Info( "Close lid" );
		float TargetPitch = 1f;
		float Speed = 6f;

		LidPitch = LidPitch.LerpTo( TargetPitch, Time.Delta * Speed );
		//LidPitch = MathF.Min( LidPitch, 0f );

		BoxLid.LocalRotation = Rotation.FromAxis( Vector3.Right, LidPitch );
		Log.Info( LidPitch );
	}





	[Rpc.Host]
	public void GoThroughWeapons()
	{
		if ( WeaponManager == null )
		{
			WeaponManager = Scene.Get<WeaponManager>();
			Log.Info( "weapon manager null" );
		}


		if ( WeaponContainer == null )
		{
			WeaponContainer = Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "MBWeaponContainer" );
			Log.Info( WeaponContainer );
		}


		if ( WeaponContainer != null )
		{
			//Log.Info( WeaponContainer.WorldPosition );
		}

		if ( TimeSinceChange >= .5f && TimeSinceOpened <= 8f )
		{
			AWeapon = WeaponManager.WeaponPaths[Random.Next( WeaponManager.WeaponPaths.Count() )];
			Log.Info( "CurrentWeapon: " + CurrentWeapon );
			Log.Info( "AWeapon: " + AWeapon );
			if ( CurrentWeapon == AWeapon )
			{
				Log.Info( "new problemo!" );
				return;
			}
			CurrentWeapon = AWeapon;

			Log.Info( "the weapon: " + AWeapon );
			var weap = GameObject.GetPrefab( AWeapon );

			var weapon = WeaponContainer.GetComponent<ModelRenderer>( true );
			if ( weapon != null )
			{
				weapon.Model = weap.GetComponentInChildren<SkinnedModelRenderer>().Model;
				weapon.Enabled = true;
				//ClientSetWeapon();
			}
			else
			{
				weapon = WeaponContainer.AddComponent<ModelRenderer>();
				weapon.Model = weap.GetComponentInChildren<SkinnedModelRenderer>().Model;
				weapon.Enabled = true;
				//ClientSetWeapon();
			}

			TimeSinceChange = 0f;
		}
		ClientSetWeapon();

		if ( TimeSinceOpened >= 8f )
		{
			Log.Info( "a given weapon: " + AWeapon );
			GivenWeapon = AWeapon;
			CanTakeWeapon = true;
			ChooseWeapon = false;
			TimeSinceFinished = 0f;
		}


	}


	[Rpc.Broadcast]
	public void ClientSetWeapon()
	{
		if ( !Networking.IsClient ) return;
		var weap = GameObject.GetPrefab( AWeapon );
		var weapon = WeaponContainer.GetComponent<ModelRenderer>( true );
		if ( weapon == null || weap == null ) return;
		Log.Info( weap );
		Log.Info( weapon.ToString() );
		
		weapon.Model = weap.GetComponentInChildren<SkinnedModelRenderer>().Model;
		weapon.Enabled = true;
	}


	[Rpc.Host]
	private void HostCycleWeapons()
	{
		
	}


	[Rpc.Broadcast]
	private void SetVarsForOthers()
	{
		
	}




	[Rpc.Host]
	public void OpenBox()
	{
		Log.Info( "open box" );
		var go = new GameObject();
		go.AddComponent<ModelRenderer>().Enabled = false;
		go.Name = "MBWeaponContainer";
		go.Tags.Add("MBWeaponContainer");
		go.NetworkSpawn();
		WeaponContainer = go;
		WeaponContainer.WorldPosition = WeaponStartPoint.WorldPosition;
		WeaponContainer.WorldRotation = Rotation.FromYaw( 90 );
		//WeaponContainer.NetworkSpawn();
		//SetVarsForOthers();
		//OpenBoxLid();
		TimeSinceOpened = 0f;
		TimeSinceChange = 0f;
		ChooseWeapon = true;
		BoxOpen = true;
	}


	[Rpc.Host]
	private void PlayerTakeWeapon(Player player, int slot)
	{
		Log.Info("giving to slot? :" + slot );
		WeaponManager.GivePlayerWeapon( GivenWeapon, player, slot );
		WeaponContainer?.Destroy();
		
	}


	[Rpc.Host]
	private void CloseBox()
	{
		CanTakeWeapon = false;
		ChooseWeapon = false;
		BoxOpen = false;
		BoxNeedsToClose = true;
	}




	public void OnInteract( Player player )
	{

		//add reject interaction


		if ( CanTakeWeapon )
		{
			Log.Info( "take weapon" );
			PlayerTakeWeapon( player, player.CurrentWeaponSlot );
			CloseBox();
			return;
		}

		if ( ChooseWeapon ) return;

		Log.Info( "open boxa" );
		OpenBox();

		
	}




}
