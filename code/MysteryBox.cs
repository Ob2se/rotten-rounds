using Sandbox;
using Sandbox.Services;
using Sandbox.UI;
using System;

public sealed class MysteryBox : Component, IInteraction
{
	[Property] public bool Hold { get; set; }
	public GameObject GO => this.GameObject;
	public float HoldTime => 0f;

	[Sync, Property] public int Cost { get; set; }
	[Property] private Collider InteractionTriggerbox { get; set; }
	[Sync] public WeaponManager WeaponManager { get; set; }
	[Sync] public bool Interactable { get; set; } = true;

	private Player playerToOpen { get; set; }
	[Sync] private string playerToOpenSteamId { get; set; }
	public Random Random { get; set; } = new Random();

	[Sync] public TimeSince TimeSinceOpened { get; set; }
	[Sync] public TimeSince TimeSinceChange { get; set; }
	[Sync] public TimeSince TimeSinceFinished { get; set; }
	private float TimeToTake { get; } = 9f;

	[Sync] public string AWeapon { get; set; }
	[Sync] public string GivenWeapon { get; set; }
	[Sync] public string CurrentWeapon { get; set; }

	[Sync] public bool ChooseWeapon { get; set; } = false;
	[Sync] public bool BoxOpen { get; set; } = false;
	[Sync] public GameObject WeaponContainer { get; set; }

	[Property] public SkinnedModelRenderer MysteryBoxMesh { get; set; }
	[Property] public GameObject WeaponStartPoint { get; set; }
	[Property] public GameObject WeaponEndPoint { get; set; }

	[Sync] public bool CanTakeWeapon { get; set; }
	private int TimesUsed;
	private float ReplaceChance = 0f;

	[Sync] public bool Replacing { get; set; } = false;
	[Sync] public bool Firesalebox { get; set; } = false;

	public List<string> playerWeaponList { get; set; } = new();


	[Property] private SoundPointComponent LatchOpenSoundPoint { get; set; }
	[Property] private SoundPointComponent BoxSpinSound { get; set; }

	private TimeSince TimeSinceShowBear { get; set; }
	[Sync] private bool BearShown { get; set; } = false;
	[Sync] private bool ShowGnomeVisual { get; set; } = false;
	public bool NeedSpot = false;
	private bool LastVisualBoxOpen = false;

	protected override void OnStart()
	{
		InteractionTriggerbox.OnObjectTriggerEnter += InteractionTriggerEnter;
		InteractionTriggerbox.OnObjectTriggerExit += InteractionTriggerExit;

		WeaponManager = Scene.GetAllComponents<WeaponManager>().FirstOrDefault();
		if ( WeaponManager == null )
		{
			Log.Info( "No WeaponManager found!" );
		}
	}

	protected override void OnUpdate()
	{
		UpdateVisualStateFromSync();

		if ( !Networking.IsHost )
			return;

		if ( Replacing && BearShown )
		{
			if ( TimeSinceShowBear >= 6f && NeedSpot )
			{
				Scene.GetAllComponents<GameModeManager>().FirstOrDefault()?.ChoseNewMBSpot();
				NeedSpot = false;
			}

			if ( TimeSinceShowBear >= 8f )
			{
				Destroythis();
			}
			return;
		}

		if ( !BoxOpen && !Interactable )
		{
			Interactable = true;
		}

		if ( ChooseWeapon && TimeSinceOpened >= .8f)
		{
			WeaponContainer.Enabled = true;
			GoThroughWeapons();
			if ( WeaponContainer != null )
			{
				WeaponContainer.WorldPosition = new Vector3(
					WeaponContainer.WorldPosition.x,
					WeaponContainer.WorldPosition.y,
					MathX.Approach( WeaponContainer.WorldPosition.z, WeaponStartPoint.WorldPosition.z + 25f, Time.Delta * 5f )
				);
			}
		}

		if ( TimeSinceFinished >= TimeToTake && CanTakeWeapon )
		{
			CloseBox();
			GivenWeapon = null;
			AWeapon = null;
			CurrentWeapon = null;

		}
	}

	private void UpdateVisualStateFromSync()
	{
		if ( MysteryBoxMesh != null && LastVisualBoxOpen != BoxOpen )
		{
			if ( BoxOpen ) MysteryBoxMesh.Set( "b_open", true );
			else MysteryBoxMesh.Set( "b_close", true );
			LastVisualBoxOpen = BoxOpen;
		}

		if ( WeaponContainer == null )
			return;

		var weaponRenderer = WeaponContainer.GetComponent<ModelRenderer>( true );
		if ( weaponRenderer == null )
		{
			weaponRenderer = WeaponContainer.AddComponent<ModelRenderer>();
		}

		// Drive gnome visual from synced state so all clients see it reliably.
		if ( ShowGnomeVisual )
		{
			var gnome = Model.Load( "models/gnome/gnomenotext.vmdl" );
			weaponRenderer.Model = gnome;
			weaponRenderer.Enabled = true;
			return;
		}

		string weaponPath = null;
		if ( CanTakeWeapon && !string.IsNullOrEmpty( GivenWeapon ) )
			weaponPath = GivenWeapon;
		else if ( ChooseWeapon && !string.IsNullOrEmpty( AWeapon ) )
			weaponPath = AWeapon;

		if ( string.IsNullOrEmpty( weaponPath ) )
			return;

		var weap = GameObject.GetPrefab( weaponPath );
		var model = weap?.GetComponentInChildren<SkinnedModelRenderer>()?.Model;
		if ( model == null )
			return;

		weaponRenderer.Model = model;
		weaponRenderer.Enabled = true;
	}

	private void InteractionTriggerEnter( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if ( player != null )
		{
			player.CurrentInteraction = this;
		}
	}

	private void InteractionTriggerExit( GameObject obj )
	{
		var player = obj.GetComponent<Player>();
		if ( player != null )
		{
			player.CurrentInteraction = null;
		}
	}

	[Rpc.Broadcast]
	private void OpenBoxLid()
	{
		MysteryBoxMesh.Set( "b_open", true );
	}

	[Rpc.Broadcast]
	private void CloseBoxLid()
	{
		MysteryBoxMesh.Set( "b_close", true );
	}

	[Rpc.Host]
	private void Destroythis()
	{
		Destroyerthis();
	}

	[Rpc.Broadcast]
	public void Destroyerthis()
	{
		GameObject.Destroy();
	}

	[Rpc.Broadcast]
	public void PlayOpenEffects()
	{
		LatchOpenSoundPoint.StartSound();
	}

	[Rpc.Host]
	public void GoThroughWeapons()
	{
		if ( WeaponManager == null )
		{
			WeaponManager = Scene.Get<WeaponManager>();
		}

		if ( WeaponContainer == null )
		{
			WeaponContainer = Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "MBWeaponContainer" );
		}

		if ( WeaponContainer == null )
			return;

		if ( TimeSinceChange >= 0.5f && TimeSinceOpened <= 8f )
		{
			AWeapon = WeaponManager.WeaponPaths[Random.Next( WeaponManager.WeaponPaths.Count() )];
			if ( CurrentWeapon == AWeapon )
				return;

			foreach ( var x in playerWeaponList )
			{
				if ( x == AWeapon )
				{

					return;
				}
			}

			CurrentWeapon = AWeapon;

			var weap = GameObject.GetPrefab( AWeapon );
			var weapon = WeaponContainer.GetComponent<ModelRenderer>( true ) ?? WeaponContainer.AddComponent<ModelRenderer>();
			weapon.Model = weap.GetComponentInChildren<SkinnedModelRenderer>().Model;
			weapon.Enabled = true;

			TimeSinceChange = 0f;
		}

		if ( TimeSinceOpened >= 8f )
		{
			if ( Replacing )
			{
				BearShown = true;
				ShowGnomeVisual = true;
				TimeSinceShowBear = 0;
				Interactable = false;
				ChooseWeapon = false;
				CanTakeWeapon = false;
				GivenWeapon = null;
				AWeapon = null;
				CurrentWeapon = null;
				NeedSpot = true;
				var gnome = Model.Load( "models/gnome/gnomenotext.vmdl" );
				var x = WeaponContainer.GetComponent<ModelRenderer>();
				x.Model = gnome;
				x.Enabled = true;
				return;
			}

			SetTakeWeapon();
		}
	}

	[Rpc.Host]
	public void SetTakeWeapon()
	{
		GivenWeapon = AWeapon;
		CanTakeWeapon = true;
		ChooseWeapon = false;
		TimeSinceFinished = 0f;
		Interactable = true;
	}

	[Rpc.Broadcast]
	public void ClientShowGnome()
	{
		var gnome = Model.Load( "models/gnome/gnomenotext.vmdl" );
		var x = WeaponContainer.GetComponent<ModelRenderer>();
		BearShown = true;
		ShowGnomeVisual = true;
		NeedSpot = true;
		TimeSinceShowBear = 0;
		Interactable = false;
		ChooseWeapon = false;
		x.Model = gnome;
		x.Enabled = true;
	}

	private string GetPlayerSteamId( Player player )
	{
		return player?.Network?.Owner?.SteamId.ToString() ?? string.Empty;
	}

	public bool CanPlayerTake( Player player )
	{
		if ( !CanTakeWeapon || player == null ) return false;
		if ( Connection.All.Count <= 1 ) return true;

		var takingSteamId = GetPlayerSteamId( player );
		if ( !string.IsNullOrEmpty( playerToOpenSteamId ) && !string.IsNullOrEmpty( takingSteamId ) )
		{
			return takingSteamId == playerToOpenSteamId;
		}

		return false;
	}

	[Rpc.Host]
	public void OpenBox( Player opener )
	{
		playerToOpen = opener;
		playerToOpenSteamId = GetPlayerSteamId( opener );
		BearShown = false;
		ShowGnomeVisual = false;
		NeedSpot = false;

		WeaponContainer?.Destroy();
		OpenBoxLid();

		var go = new GameObject();
		go.Parent = this.GameObject;
		go.AddComponent<ModelRenderer>().Enabled = false;
		go.Name = "MBWeaponContainer";
		go.Tags.Add( "MBWeaponContainer" );
		go.NetworkSpawn();
		WeaponContainer = go;
		WeaponContainer.WorldTransform = WeaponStartPoint.WorldTransform;
		WeaponContainer.Enabled = false;

		TimeSinceOpened = 0f;
		TimeSinceChange = 0f;
		ChooseWeapon = true;
		BoxOpen = true;
		Interactable = false;

		if ( !Scene.GetAllComponents<GameModeManager>().FirstOrDefault().FireSaleStarted )
		{
			ReplaceChance = Math.Clamp( TimesUsed * 3, 0, 50 );
			Replacing = RollChance( ReplaceChance );
			TimesUsed += 1;
		}

		PlayOpenEffects();
	}

	private bool RollChance( float percent )
	{
		percent = Math.Clamp( percent, 0f, 100f );
		float roll = Random.Shared.NextSingle() * 100f;
		return roll < percent;
	}

	private bool IsOpeningPlayer( Player player )
	{
		if ( player == null ) return false;
		if ( Connection.All.Count <= 1 ) return true;

		var takingSteamId = GetPlayerSteamId( player );
		if ( !string.IsNullOrEmpty( playerToOpenSteamId ) && !string.IsNullOrEmpty( takingSteamId ) )
			return takingSteamId == playerToOpenSteamId;

		if ( playerToOpen != null )
		{
			if ( playerToOpen == player ) return true;
			if ( playerToOpen.GameObject == player.GameObject ) return true;
		}

		// If network identity isn't available on either side, don't hard-block take.
		// Better to allow a take than permanently soft-lock the box.
		if ( string.IsNullOrEmpty( playerToOpenSteamId ) || string.IsNullOrEmpty( takingSteamId ) )
			return true;

		return false;
	}

	[Rpc.Host]
	private void PlayerTakeWeapon( Player player, int slot )
	{
		if ( !CanTakeWeapon || !IsOpeningPlayer( player ) )
			return;

		GiveWeapon( player );
		player.PlayChaChing();
		CloseBox();
	}

	private void GiveWeapon( Player player )
	{
		player.Inventory.AddWeapon( GivenWeapon, 0 );
	}

	[Rpc.Host]
	private void CloseBox()
	{
		WeaponContainer?.Destroy();
		CanTakeWeapon = false;
		ChooseWeapon = false;
		BoxOpen = false;
		BearShown = false;
		ShowGnomeVisual = false;
		NeedSpot = false;
		Interactable = true;
		playerToOpen = null;
		playerToOpenSteamId = string.Empty;
		CloseBoxLid();
	}

	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
	}


	[Rpc.Host]
	private void SetWeaponList(List<string> weapons)
	{
		playerWeaponList = weapons;
	}


	public void OnInteract( Player player )
	{
		if ( !Interactable )
			return;

		if ( CanTakeWeapon )
		{
			PlayerTakeWeapon( player, player.CurrentWeaponSlot );
			return;
		}

		if ( ChooseWeapon ) return;
		if ( BoxOpen && !CanTakeWeapon ) return;

		if ( player.Points < Cost )
		{
			return;
		}

		OpenBox( player );
		playerWeaponList.Clear();
		SetWeaponList( player.Inventory.Weapons.Select( x => x.Weapon ).ToList() );
		player.RemovePoints( Cost );
	}
}
