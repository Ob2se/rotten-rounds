using Sandbox;
using Sandbox.Citizen;
using Sandbox.UI;
using System;
using System.Net.Http.Headers;
using System.Numerics;
using System.Threading.Tasks;
using static Sandbox.Citizen.CitizenAnimationHelper;
using static Sandbox.Package;
using static Sandbox.PhysicsContact;



public sealed class Player : Component, IHudInterface, IInteraction
{
	public struct ChatMessageData
	{
		public string SteamId { get; set; }
		public string PlayerName { get; set; }
		public string Message { get; set; }

		public ChatMessageData( string steamId, string playerName, string message )
		{
			SteamId = steamId;
			PlayerName = playerName;
			Message = message;
		}
	}

	public static event Action<ChatMessageData> ChatMessageReceived;

	[RequireComponent]
	[Sync]
	public PlayerController PlayerController { get; set; }

	[Property]
	private SoundPointComponent ChaChingSound { get; set; }

	public GameObject GO => this.GameObject;


	[Property]
	public Dresser PlayerDresser { get; set; }


	public float HoldTime => 6f;
	public bool Interactable { get; set; } = false;

	[Property]
	public bool Hold { get; set; } = true;


	bool HoldingInteraction { get; set; } = false;
	IInteraction HeldInteractionTarget { get; set; }
	private const float InteractionSelectRange = 35f;
	[Property] private bool DebugInteractionSelection { get; set; } = true;
	ClothingContainer ClothingContainer { get; set; }

	[Property] public SkinnedModelRenderer Body { get; set; }

	[Property] public SkinnedModelRenderer fpsArms { get; set; }

	[Property] GameObject WeaponContainer { get; set; }

	[Property, Sync] public GameObject ThirdPersonWeaponModelContainer { get; set; }

	public float BaseHeight { get; } = 72f;

	[Property, Sync]
	public ModelRenderer ThirdPersonWeaponModel { get; set; }

	[Property] public CameraComponent playerCamera { get; set; }

	CitizenAnimationHelper HandsAnimationHelper;

	public Connection PlayerConnection => Connection.Local;

	public CitizenAnimationHelper PlayerAnimationHelper { get; set; }

	private WeaponManager WeaponManager { get; set; }

	[Sync]
	public GameObject CurrentWeapon { get; set; }

	[Sync]
	public CitizenAnimationHelper.HoldTypes HoldType { get; set; }

	[Property]
	private float BaseRunSpeed { get; set; } = 200f;


	[Property]
	private SkinnedModelRenderer Knife { get; set; }

	[Property]
	private SkinnedModelRenderer KnifeHand { get; set; }

	[Property]
	private float BaseWalkSpeed { get; set; } = 200f;
	public int CurrentWeaponType { get; set; }

	GameObject WeaponPrefab { get; set; }

	[Sync]
	public int CurrentWeaponSlot { get; set; }

	[Sync] SkinnedModelRenderer WeaponModel { get; set; }

	[Sync] public float Health { get; set; }

	[Sync] public float MaxHealth { get; set; } = 100f;

	[Sync, Change( "PointsChanged" )] public float Points { get; set; }


	public event Action<float> PlayerPointsChanged;

	public event Action PerkListChanged;


	public static event Action AnyPlayerPointsChanged;


	public List<GameObject> ObjectsInCurrentKnife = new();

	public bool isReviving { get; set; } = false;

	[Sync] public bool Downed { get; set; } = false;

	[Property]
	public InventoryComponent Inventory { get; set; }

	[Property]
	public PanelComponent Hud { get; set; }

	[Property]
	public PanelComponent SniperScope { get; set; }

	[Property]
	private PrefabFile BloodMistMelee { get; set; }




	public bool isKnifing = false;
	public bool isAiming = false;

	public bool isDeploying = false;


	public bool isSprinting { get; set; } = false;

	public bool isWalking { get; set; } = false;
	public bool isFiring { get; set; } = false;

	[Property]
	private Sandbox.WorldPanel NameTag { get; set; }
	public bool CanShootWeapon => CanShoot();

	[Sync]
	public BaseWeapon CurrentWeaponClass { get; set; }


	public string WeaponName => CurrentWeaponClass != null ? CurrentWeaponClass.WeaponName : "None";

	public int AmmoInMag => Inventory.Weapons.Count > 0 ? Inventory.WeaponsAmmo[CurrentWeaponSlot].CurrentMag : 0;

	public int AmmoTotal => Inventory.Weapons.Count > 0 ? Inventory.WeaponsAmmo[CurrentWeaponSlot].AmmoTotal : 0;

	public List<Player> PlayerList { get; set; } = new();

	Angles lastEyeAngles;
	float aimPitchInertia;
	float aimYawInertia;

	Rotation lastViewRot;

	public List<string> PerkIcons { get; set; } = new();


	public bool LookingAtInteractable { get; set; } = false;
	public bool LookingAtInteractableRevive { get; set; } = false;
	public bool LookingAtInteractableHold { get; set; } = false;
	public string InteractionPromptText { get; private set; } = string.Empty;
	public string InteractionCostText { get; private set; } = string.Empty;

	public float CurrentInterHoldTime = 0f;

	public IInteraction CurrentInteraction { get; set; }

	[Sync]
	public float ReviveTime { get; set; } = 7f;

	[Sync]
	public bool Reviving { get; set; } = false;

	[Sync]
	private Player ReviveTarget { get; set; }

	[Sync]
	public bool IsUsingVoiceChat { get; set; }

	public bool IsTypingChat { get; set; }

	private bool LastVoiceChatState { get; set; }

	private float BaseCameraHeight;

	[Property]
	public BoxCollider ReviveBox { get; set; }

	public TimeSince TimeSinceInteractHeld { get; set; }

	TimeSince TimeSinceDamagedLast { get; set; }

	TimeSince TimeSinceLastRegen { get; set; }

	TimeSince TimeSinceKnifeStarted;
	TimeSince TimeSinceLastStackNudge;
	bool KnifeAttackTriggered;
	bool KnifeMeshShown;

	TimeSince TimeSinceLastInteractionPress = 0f;
	const float InteractionPressCooldown = 0.1f;

	TimeSince TimeSinceWeaponSwap = 0f;
	TimeSince TimeSinceLastGrenadeThrow;

	public GameObject TPWeapon;

	public bool HasQuickRevive = false;



	[Rpc.Owner]
	private void SetWeaponPosition()
	{
		if ( IsProxy ) return;


		CurrentWeaponClass.WeaponModel.RenderType = ModelRenderer.ShadowRenderType.Off;


		CurrentWeapon.SetParent( fpsArms.GameObject, false );

		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped( CurrentWeapon ) );


		var boneObject = fpsArms.GetBoneObject( "weapon_IK_hand_R" );
		var bw = CurrentWeapon.GetComponentInChildren<BaseWeapon>();

		fpsArms.BoneMergeTarget = bw.WeaponModel;


	}

	public void UpdatePlayerListt( List<Player> newList )
	{

	}


	public async Task PerkListIconsChanged()
	{

		await Task.Delay( 100 );
		PerkListChanged?.Invoke();
	}

	public void PointsChanged( float oldValue, float newValue )
	{
		var changeAmount = newValue - oldValue;
		PlayerPointsChanged?.Invoke( changeAmount );
		AnyPlayerPointsChanged?.Invoke();
		//PlayChaChing();
	}


	public void AddToPlayerList( Connection connection, Player player )
	{
		PlayerList.Add( player );
		Log.Info( connection + " | " + player );
	}

	[Rpc.Host]
	public void SubmitChatMessage( string steamId, string playerName, string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
		{
			return;
		}

		var trimmed = message.Trim();
		if ( trimmed.Length > 120 )
		{
			trimmed = trimmed[..120];
		}

		BroadcastChatMessage( steamId, playerName, trimmed );
	}

	[Rpc.Broadcast]
	private void BroadcastChatMessage( string steamId, string playerName, string message )
	{
		ChatMessageReceived?.Invoke( new ChatMessageData( steamId, playerName, message ) );
	}

	[Rpc.Host]
	private void SetVoiceChatState( bool isVoiceActive )
	{
		BroadcastVoiceChatState( isVoiceActive );
	}

	[Rpc.Broadcast]
	private void BroadcastVoiceChatState( bool isVoiceActive )
	{
		IsUsingVoiceChat = isVoiceActive;
	}

	private void DestroyWeapon()
	{
		CurrentWeapon?.Destroy();
	}



	[Rpc.Host]
	public void RemoveHealth( float amount )
	{
		ChangeHealthNeg( amount );
		TakenDamageFeedback();
		if ( this.Health <= 0 )
		{
			PlayerDowned();
		}
		Log.Info( Health );
	}

	[Rpc.Broadcast]
	private void ChangeHealthNeg( float amount )
	{
		this.Health = Math.Clamp( this.Health - amount, 0, this.MaxHealth );
		this.Body.Set( "hit_strength", 1f );
		this.Body.Set( "hit", true );
		TimeSinceDamagedLast = 0f;
	}

	[Rpc.Broadcast]
	private void ChangeHealthPos( float amount )
	{
		this.Health = Math.Clamp( this.Health + amount, 0, this.MaxHealth );
	}

	[Rpc.Host]
	public void AddPoints( int points )
	{
		ChangePointsPos( points );

	}

	[Rpc.Broadcast]
	private void ChangePointsPos( int points )
	{
		this.Points += points;
	}

	[Rpc.Broadcast]
	private void ChangeHealthToMax()
	{
		this.Health = this.MaxHealth;
	}


	[Rpc.Host]
	public void RemovePoints( int points )
	{
		CheckPoints( points );
	}

	[Rpc.Host]
	private void CheckPoints( int points )
	{
		if ( points <= 0 )
		{
			return;
		}

		var check = this.Points - points < 0;
		ChangePointsNeg( check, points );

	}



	public bool CheckPointsInteraction( int points )
	{
		if ( points <= 0 )
		{
			return false;
		}

		var check = this.Points - points < 0;
		return check;
	}


	[Rpc.Broadcast]
	private void ChangePointsNeg( bool check, int points )
	{
		if ( !check && points > 0 )
		{
			this.Points -= points;

		}
	}



	[Rpc.Owner]
	private void SpawnWeapon()
	{

		CurrentWeapon?.Destroy();
		var weap = GameObject.GetPrefab( Inventory.Weapons[CurrentWeaponSlot] );
		var weapon = weap.Clone();
		CurrentWeapon = weapon;
		//CurrentWeapon.NetworkMode = NetworkMode.Never;
		CurrentWeaponClass = weapon.GetComponentInChildren<BaseWeapon>();

		SetWeaponPosition();
		Thefuckifiknow( CurrentWeaponClass.WeaponModel.Model.ResourcePath );
		//weapon.Destroy();



	}


	[Rpc.Owner]
	private void ClientWeaponEquipped()
	{
		//fpsArms.AnimationGraph = CurrentWeapon.GetComponentInChildren<BaseWeapon>().WeaponModel.AnimationGraph;
		Scene.RunEvent<IWeaponHandler>( x => x.WeaponEquipped( CurrentWeapon ) );

	}

	[Rpc.Host]
	private void ServerChangeThidPersonChar()
	{
		Log.Info( Connection.Local.DisplayName + " changing weapon" );
		ChangeThirdPersonChar( this, CurrentWeapon );
	}



	//this is for changing the weapon that appears for other players, purely cosmetic for others, not seen by owner[[[
	//[Rpc.Broadcast]
	public void ChangeThirdPersonChar( Player player, GameObject weapon )
	{
		if ( CurrentWeapon != null )
		{
			Thefuckifiknow( CurrentWeaponClass.WeaponModel.Model.ResourcePath );
			//ManageHoldType(1);
		}
	}

	[Rpc.Host]
	private void Thefuckifiknow( string weaponPath )
	{

		if ( ThirdPersonWeaponModelContainer.Children.Count > 0 )
		{
			foreach ( var x in ThirdPersonWeaponModelContainer.Children )
			{
				x.Destroy();
			}
		}

		var go = new GameObject();
		go.AddComponent<ModelRenderer>().Model = Model.Load( weaponPath );

		go.Parent = ThirdPersonWeaponModelContainer;
		go.WorldRotation = ThirdPersonWeaponModelContainer.WorldRotation;
		go.NetworkSpawn();



	}

	[Rpc.Broadcast]
	public void ManageHoldType( int x )
	{

		Body.Set( "holdtype", x );

	}


	[Rpc.Broadcast]
	public void UpdatePlayerList( List<Player> newList )
	{
		PlayerList?.Clear();
		PlayerList = newList;
	}




	protected override void OnStart()
	{
		base.OnStart();

		PlayerAnimationHelper = new CitizenAnimationHelper();
		PlayerAnimationHelper.Target = PlayerController.Renderer;

		PlayerDresser.Apply();


		ReviveBox.OnObjectTriggerEnter += InteractionReviveTriggerEnter;
		ReviveBox.OnObjectTriggerExit += InteractionReviveTriggerExit;
		Knife.OnAnimTagEvent += tag => HandleAnimEvent( tag );


		if ( IsProxy )
		{
			playerCamera.Enabled = false;
			fpsArms.Enabled = false;
			Hud.Enabled = false;


		}
		else
		{

			playerCamera.Enabled = true;
			fpsArms.Enabled = true;
			Hud.Enabled = true;
			Log.Info( PlayerList.Count );
			SetHealthToMax();




		}

		Log.Info( "player spawned" );



		StartingValues();
	}





	[Rpc.Host]
	public void SetHealthToMax()
	{
		ChangeHealthToMax();
	}


	public void ChangeCurrentSlot( int slot )
	{
		TimeSinceWeaponSwap = 0;
		isDeploying = true;
		CurrentWeaponSlot = slot;
		SpawnWeapon();
	}


	[Rpc.Host]
	private void StartingValues()
	{

		if ( Points != 0 && Health != 0 ) return;
		/*SetHealthToMax();
		AddPoints( 500 );*/

	}







	[Rpc.Owner]
	public void SetVMVisibility( bool visibility )
	{
		fpsArms.Enabled = visibility;

	}


	private SceneTraceResult? LineTrace()
	{
		var cameraPos = playerCamera.WorldPosition;
		var direction = playerCamera.WorldRotation.Forward;

		var endPosition = cameraPos + direction * 100;

		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).WithTag( "interactable" ).Run();

		if ( traceResult.Hit )
		{
			return traceResult;
		}
		else
		{
			return null;
		}
	}




	private void LineTraceMelee()
	{
		var cameraPos = Knife.WorldPosition;
		var direction = playerCamera.WorldRotation.Forward;

		var endPosition = cameraPos + direction * 65f;
		//DebugOverlay.Line( cameraPos, endPosition, Color.Red, 5f );
		var traceResult = Scene.Trace.Ray( cameraPos, endPosition ).IgnoreGameObject( GameObject ).WithTag( "zombie" ).WithTag( "solid" ).Run();

		if ( traceResult.Hit )
		{
			var knifedamage = new DamageInfo();
			knifedamage.Damage = 50f;
			knifedamage.Attacker = this.GameObject;
			var incDir = (traceResult.HitPosition - GameObject.WorldPosition).Normal;
			var dir = (incDir - traceResult.HitPosition.Normal * 1f).Normal;
			traceResult.GameObject.Parent.GetComponent<Zombie>()?.TakeKnifeDamage( this, traceResult.HitPosition, dir, GameObject.Name, 5000 );
			GameObject.GetPrefab( BloodMistMelee.ResourcePath ).Clone( traceResult.HitPosition );
		}

	}



	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		//could add some feedback here later
	}

	public void OnInteract( Player player )
	{
		if ( !Downed ) return;
		Log.Info( "oninteract2" );
		player.CurrentInteraction = null;
		RevivePlayer();

	}



	[Rpc.Host]
	public void RevivePlayer()
	{
		if ( !Downed ) { return; }
		SetPlayerUps();

	}

	[Rpc.Broadcast]
	private void SetPlayerUps()
	{
		if ( this.Downed )
		{
			this.Downed = false;
		}

		this.Body.Set( "special_movement_states", 4 );
		//this.PlayerController.IsDucking = false;
		this.PlayerController.WalkSpeed = BaseWalkSpeed;
		this.PlayerController.RunSpeed = BaseRunSpeed;
		this.PlayerController.BodyHeight = BaseHeight;
		this.PlayerController.JumpSpeed = 290;
		if ( ReviveBox != null )
		{
			ReviveBox.Enabled = false;
			if ( ReviveBox.Tags.Contains( "interactable" ) ) ReviveBox.Tags.Remove( "interactable" );

			if ( this.GameObject.Tags.Contains( "downed" ) )
			{
				this.GameObject.Tags.Remove( "downed" );
			}
		}


	}


	public static byte ClampToByte( float value, float min, float max )
	{
		if ( value < min ) value = min;
		if ( value > max ) value = max;
		return (byte)((value - min) / (max - min) * 255f);
	}


	[Rpc.Owner]
	private void TakenDamageFeedback()
	{
		Sound.Play( "sound/hits/hitsounds.sound", GameObject.WorldPosition );
		playerCamera.WorldRotation = new Rotation( -100, -100, -100, 1 );
	}


	[Rpc.Host]
	private void PlayerDowned()
	{
		SetPlayerDowns();
	}

	[Rpc.Broadcast]
	private void SetPlayerDowns()
	{

		this.Downed = true;
		Body.Set( "special_movement_states", 3 );
		PlayerController.WalkSpeed = 25f;
		PlayerController.DuckedSpeed = 25f;
		//PlayerController.IsDucking = true;
		PlayerController.JumpSpeed = 0;
		this.Interactable = true;
		this.PlayerController.BodyHeight = 30;
		if ( this.ReviveBox != null )
		{
			this.ReviveBox.Enabled = true;
			if ( !this.ReviveBox.Tags.Contains( "interactable" ) )
			{
				this.ReviveBox.Tags.Add( "interactable" );
			}
		}
		GameObject.Tags.Add( "downed" );
	}

	public void UpdateWeaponSway()
	{


		Rotation viewRot = Scene.Camera.WorldRotation.Angles();
		Rotation deltaRot = viewRot * lastViewRot.Inverse;
		Angles deltaAngles = deltaRot.Angles();

		// Convert deltaAngles to per-second (angular velocity)
		Angles angularVelocity = deltaAngles / Time.Delta;

		// Scale sway amplitude
		float swayScale = 0.015f; // adjust this to taste
		aimPitchInertia = aimPitchInertia.LerpTo( angularVelocity.pitch * swayScale, Time.Delta * 15f );
		aimYawInertia = aimYawInertia.LerpTo( angularVelocity.yaw * swayScale, Time.Delta * 15f );

		CurrentWeaponClass.WeaponModel.Set( "aim_pitch_inertia", aimPitchInertia );
		CurrentWeaponClass.WeaponModel.Set( "aim_yaw_inertia", aimYawInertia );

		lastViewRot = viewRot;
	}




	private void ControlMovingAnimationFPS()
	{

		CurrentWeaponClass.WeaponModel.Set( "move_bob", PlayerController.Velocity.WithZ( 0 ).Length );


		if ( isSprinting )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_sprint", true );
		}
		if ( !isSprinting )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_sprint", false );
		}
		if ( Input.Pressed( "Jump" ) )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_jump", true );
		}
		if ( PlayerController.IsAirborne )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_grounded", false );
		}
		if ( !PlayerController.IsAirborne )
		{
			CurrentWeaponClass.WeaponModel.Set( "b_grounded", true );
		}

	}


	[Rpc.Host]
	private void RegenHealth()
	{

		ChangeHealthPos( .5f );



	}


	[Rpc.Host]
	public void DownedAnimControlHost()
	{
		DownedAnimControl();
	}

	[Rpc.Broadcast]
	private void DownedAnimControl()
	{

		Body.Set( "move_direction", 250 );
		Body.Set( "move_groundspeed", 1000 );
		Body.Set( "move_speed", 1000 );
	}



	private void InteractionTags( SceneTraceResult traceResult )
	{
		var inter = traceResult.GameObject.Components.GetAll<IInteraction>().FirstOrDefault();
		if ( traceResult.GameObject.Tags.Has( "downed" ) )
		{
			inter.OnInteract( traceResult.GameObject.GetComponent<Player>() );
			return;
		}


		inter.OnInteract( this );
	}

	[Rpc.Owner]
	private void RevivingPlayer()
	{
		ReviveTarget.RevivePlayer();
	}



	private void InteractionUITick()
	{

		if ( CurrentInteraction == null || !CurrentInteraction.Interactable )
		{
			LookingAtInteractable = false;

			LookingAtInteractableRevive = false;

			LookingAtInteractableHold = false;
			InteractionPromptText = string.Empty;
			InteractionCostText = string.Empty;


			return;

		}

		if ( !UpdateInteractionContext( CurrentInteraction ) )
		{
			LookingAtInteractable = false;
			LookingAtInteractableRevive = false;
			LookingAtInteractableHold = false;
			InteractionPromptText = string.Empty;
			InteractionCostText = string.Empty;
			return;
		}



		if ( CurrentInteraction.Interactable )
		{

			if ( CurrentInteraction.Hold )
			{
				if ( ReviveTarget == null )
				{

				}
				if ( ReviveTarget != null )
				{
					LookingAtInteractableRevive = true;

					return;
				}

				LookingAtInteractableHold = true;
				return;


			}
			else
			{
				LookingAtInteractable = true;
				return;
			}
		}


	}


	private void ConstInterTrace()
	{
		var useTrace = LineTrace();
		if ( useTrace == null )
		{
			LookingAtInteractable = false;
			LookingAtInteractableRevive = false;
			LookingAtInteractableHold = false;

			return;
		}
		if ( useTrace.HasValue )
		{
			if ( useTrace.Value.Tags.Contains( "downed" ) )
			{
				LookingAtInteractableRevive = true;
				return;
			}
			if ( useTrace.Value.Tags.Contains( "interactablehold" ) )
			{
				LookingAtInteractableHold = true;
				return;
			}
			LookingAtInteractable = true;
			return;
		}
		return;
	}


	[Rpc.Owner]
	public void PlayerKnife()
	{
		if ( CurrentWeaponClass?.WeaponModel == null || Knife == null )
		{
			return;
		}

		TimeSinceKnifeStarted = 0;
		KnifeAttackTriggered = false;
		KnifeMeshShown = false;
		Knife.Set( "b_attack", false );
		CurrentWeaponClass.WeaponModel.Set( "b_deploy_skip", true );
		CurrentWeaponClass.WeaponModel.Set( "speed_deploy", 5f );
		CurrentWeaponClass.WeaponModel.Set( "b_lower_weapon", true );
		CurrentWeaponClass.WeaponModel.RenderOptions.Game = true;
		fpsArms.RenderOptions.Game = true;
		Knife.RenderOptions.Game = false;
		if ( KnifeHand != null )
		{
			KnifeHand.RenderOptions.Game = false;
		}
		isKnifing = true;
		LineTraceMelee();
	}

	

	private void FinishKnife()
	{
		if ( CurrentWeaponClass?.WeaponModel == null || Knife == null )
		{
			isKnifing = false;
			return;
		}

		Knife.Set( "b_attack", false );
		Knife.RenderOptions.Game = false;
		if ( KnifeHand != null )
		{
			KnifeHand.RenderOptions.Game = false;
		}

		CurrentWeaponClass.WeaponModel.Set( "b_deploy_skip", false );
		CurrentWeaponClass.WeaponModel.Set( "speed_deploy", 10f );
		CurrentWeaponClass.WeaponModel.Set( "b_lower_weapon", false );
		CurrentWeaponClass.WeaponModel.RenderOptions.Game = true;
		fpsArms.RenderOptions.Game = true;

		TimeSinceWeaponSwap = 0f;
		isDeploying = true;
		isKnifing = false;
		KnifeAttackTriggered = false;
		KnifeMeshShown = false;
	}


	[Rpc.Owner]
	private void HandleAnimEvent( SceneModel.AnimTagEvent action )
	{
		switch ( action.Status )
		{
			case SceneModel.AnimTagStatus.Start:

				/*isKnifing = true;
				TimeSinceKnifeStarted = 0;*/
				break;
			case SceneModel.AnimTagStatus.End:


				break;
		}
	}


	[Rpc.Owner]
	public void PlayChaChing()
	{
		ChaChingSound.StartSound();
	}


	private bool CanShoot()
	{
		if ( IsTypingChat )
		{
			return false;
		}


		if ( isKnifing )
		{
			return false;
		}

		if ( isDeploying )
		{
			return false;
		}

		return true;
	}

	private static string SplitPascalCase( string input )
	{
		if ( string.IsNullOrWhiteSpace( input ) ) return string.Empty;
		return System.Text.RegularExpressions.Regex.Replace( input, "(\\B[A-Z])", " $1" );
	}

	private bool UpdateInteractionContext( IInteraction interaction )
	{
		string action = "Use";
		string target = "Object";
		int? cost = null;

		switch ( interaction )
		{


			case Player downedPlayer when downedPlayer.Downed:
				action = "Hold";
				target = "Revive";
				break;
			case MysteryBox box:
				if ( box.CanTakeWeapon )
				{
					if ( box.CanPlayerTake( this ) )
					{
						action = "Take";
						target = "Weapon";
					}
					else
					{
						InteractionPromptText = string.Empty;
						InteractionCostText = string.Empty;
						return false;
					}
				}
				else
				{
					action = "Open";
					target = "Mystery Box";
					cost = box.Cost;
				}
				break;
			case Door door:
				if ( !door.Powered && door.NeedsPower )
				{
					action = "Requires";
					target = "Power...";
					break;
				}
				action = "Open";
				target = "Door";
				cost = door.DoorPrice;
				break;
			case PerkMachine perk:
				if ( !perk.hasPower )
				{
					action = "Requires";
					target = "Power...";
					break;
				}
				action = "Buy";
				target = SplitPascalCase( perk.PerkType.ToString() );
				cost = perk.Cost;
				break;
			case WallBuy wallBuy:
				action = "Buy";
				target = string.IsNullOrWhiteSpace( wallBuy.WeaponName ) ? "Weapon" : wallBuy.WeaponName;
				cost = wallBuy.Cost;
				break;
			case PackAPunch packAPunch:
				action = "Pack-a-Punch";
				target = "Weapon";
				cost = 5000;
				break;
			case Power:
				action = "Turn On";
				target = "Power";
				break;
			case Window:
				action = "Hold";
				target = "Repair Barrier";
				break;

			case Teleporter teleporter:
				if ( !teleporter.Powered )
				{
					action = "Requires";
					target = "Power...";
					break;
				}
				if ( teleporter.LinkStarted )
				{
					action = "Waiting";
					target = "for link...";
					break;
				}
				if ( !teleporter.TeleporterActivated )
				{
					action = "Link";
					target = "Teleporter";
					break;
				}
				if ( teleporter.TeleporterActivated && !teleporter.TeleporterActive )
				{
					action = "Teleporter";
					target = "Cooling...";
					break;
				}
				if ( teleporter.TeleporterActivated && teleporter.TeleporterActive )
				{
					action = "Use";
					target = "Teleporter";
					break;
				}
				break;
			case TeleporterLink teleporterlink:
				if ( teleporterlink.LinkStarted )
				{
					action = "Link";
					target = "Teleporter";
					break;
				}
				if ( !teleporterlink.LinkStarted )
				{
					action = "Waiting";
					target = "for link...";
					break;
				}
				break;
			default:
				target = SplitPascalCase( interaction.GetType().Name );
				break;
		}

		InteractionPromptText = $"{action} {target}".Trim();
		InteractionCostText = cost.HasValue ? $"${cost.Value}" : string.Empty;
		return true;
	}

	private int GetInteractionPriority( IInteraction interaction )
	{
		if ( interaction is Player ) return 0; // revive first
		if ( interaction is Window ) return 1;
		if ( interaction is Door ) return 2;
		if ( interaction is Power ) return 3;
		if ( interaction is PerkMachine ) return 4;
		if ( interaction is WallBuy ) return 5;
		if ( interaction is MysteryBox ) return 6;
		if ( interaction is PackAPunch ) return 7;
		return 10;
	}

	private void DrawInteractionDebug()
	{
		var center = GameObject.WorldPosition + Vector3.Up * 4f;
		Color ringColor = HoldingInteraction ? Color.Yellow : Color.Cyan;
		DebugOverlay.Sphere( new Sphere( center, InteractionSelectRange ), ringColor, 5f );
		DebugOverlay.Line( center, center + Vector3.Up * 80f, Color.Red, 5f );

		if ( CurrentInteraction != null && CurrentInteraction.GO != null && CurrentInteraction.GO.IsValid() )
		{
			var targetPos = CurrentInteraction.GO.WorldPosition + Vector3.Up * 20f;
			DebugOverlay.Line( center, targetPos, Color.Green, 5f );
		}
	}



	private bool IsValidInteractionCandidate( IInteraction interaction, out float distanceSquared )
	{
		distanceSquared = float.MaxValue;
		if ( interaction == null || interaction == this ) return false;
		if ( interaction.GO == null || !interaction.GO.IsValid() ) return false;

		if ( interaction is Player targetPlayer )
		{
			if ( targetPlayer == this || !targetPlayer.Downed ) return false;
		}
		else
		{
			if ( !interaction.Interactable ) return false;
		}

		var playerPos = GameObject.WorldPosition;
		Vector3 nearestPoint = interaction.GO.WorldPosition;
		bool foundTaggedCollider = false;
		bool foundAnyCollider = false;
		float nearestAnyDistance = float.MaxValue;
		float nearestTaggedDistance = float.MaxValue;

		// Prefer colliders tagged as interactable, but fall back to any collider.
		foreach ( var collider in interaction.GO.GetComponentsInChildren<Collider>() )
		{
			if ( collider == null || !collider.Enabled )
				continue;

			Vector3 candidatePoint = collider.GameObject.WorldPosition;
			if ( collider is BoxCollider box )
			{
				var worldCenter = collider.GameObject.WorldPosition + (collider.GameObject.WorldRotation * box.Center);
				var localPos = collider.GameObject.WorldRotation.Inverse * (playerPos - worldCenter);
				var halfExtents = box.Scale * 0.5f;
				var clampedLocal = new Vector3(
					Math.Clamp( localPos.x, -halfExtents.x, halfExtents.x ),
					Math.Clamp( localPos.y, -halfExtents.y, halfExtents.y ),
					Math.Clamp( localPos.z, -halfExtents.z, halfExtents.z )
				);
				candidatePoint = worldCenter + (collider.GameObject.WorldRotation * clampedLocal);
			}

			var candidateDistance = candidatePoint.DistanceSquared( playerPos );
			if ( candidateDistance < nearestAnyDistance )
			{
				foundAnyCollider = true;
				nearestAnyDistance = candidateDistance;
				if ( !foundTaggedCollider )
				{
					nearestPoint = candidatePoint;
				}
			}

			if ( collider.Tags.Contains( "interactable" ) && candidateDistance < nearestTaggedDistance )
			{
				foundTaggedCollider = true;
				nearestTaggedDistance = candidateDistance;
				nearestPoint = candidatePoint;
			}
		}

		if ( interaction is Window && !foundTaggedCollider )
			return false;

		if ( interaction is Player && !foundTaggedCollider )
			return false;

		if ( !foundTaggedCollider && foundAnyCollider )
		{
			distanceSquared = nearestAnyDistance;
			return distanceSquared <= (InteractionSelectRange * InteractionSelectRange);
		}

		distanceSquared = nearestPoint.DistanceSquared( playerPos );
		return distanceSquared <= (InteractionSelectRange * InteractionSelectRange);
	}

	private void UpdateInteractionSelection()
	{
		// Keep a locked target while holding, unless it becomes invalid.
		if ( HoldingInteraction && HeldInteractionTarget != null )
		{
			if ( IsValidInteractionCandidate( HeldInteractionTarget, out _ ) )
			{
				CurrentInteraction = HeldInteractionTarget;
			}
			else
			{
				HoldingInteraction = false;
				HeldInteractionTarget = null;
				CurrentInteraction = null;
			}
		}

		if ( !HoldingInteraction )
		{
			IInteraction best = null;
			int bestPriority = int.MaxValue;
			float bestDistance = float.MaxValue;

			foreach ( var interaction in Scene.GetAllComponents<IInteraction>() )
			{
				if ( !IsValidInteractionCandidate( interaction, out var distanceSquared ) )
					continue;

				int priority = GetInteractionPriority( interaction );
				if ( priority < bestPriority || (priority == bestPriority && distanceSquared < bestDistance) )
				{
					best = interaction;
					bestPriority = priority;
					bestDistance = distanceSquared;
				}
			}

			CurrentInteraction = best;
		}

		// Keep revive state in sync with selected target.
		if ( CurrentInteraction is Player revivePlayer && revivePlayer.Downed )
		{
			ReviveTarget = revivePlayer;
			isReviving = true;
		}
		else if ( !HoldingInteraction )
		{
			ReviveTarget = null;
			isReviving = false;
		}
	}

	private void UpdateVoiceState()
	{

	}




	private void ControlCamera()
	{
		if ( !IsProxy && playerCamera != null )
		{

			if ( Body.RenderType != ModelRenderer.ShadowRenderType.ShadowsOnly )
			{
				foreach ( var x in Body.GetComponentsInChildren<SkinnedModelRenderer>() )
				{
					if ( x.RenderType != ModelRenderer.ShadowRenderType.ShadowsOnly )
					{
						x.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
					}
				}

				if ( ThirdPersonWeaponModel != null )
				{
					ThirdPersonWeaponModel.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
				}

			}
			var ee = PlayerController.EyeAngles;
			ee += Input.AnalogLook * Preferences.Sensitivity;
			ee.roll = 0;

			if ( PlayerController != null )
			{
				if ( PlayerController.IsDucking )
				{

					playerCamera.LocalPosition = new Vector3( playerCamera.LocalPosition.x, playerCamera.LocalPosition.y, MathX.Approach( playerCamera.LocalPosition.z, PlayerController.DuckedHeight, Time.Delta * 200f ) );
				}

				if ( !PlayerController.IsDucking )
				{
					playerCamera.LocalPosition = new Vector3( playerCamera.LocalPosition.x, playerCamera.LocalPosition.y, MathX.Approach( playerCamera.LocalPosition.z, 70, Time.Delta * 200f ) );
				}
			}


			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();

			var cambone = fpsArms.GetBoneObject( "camera" );
			if ( cambone != null )
			{
				//ee += cambone.LocalRotation.Angles();

			}
			PlayerController.EyeAngles = ee;
			var lookDir = PlayerController.EyeAngles.ToRotation();

			playerCamera.WorldRotation = lookDir;
		}
	}

	private void InteractionReviveTriggerEnter( GameObject obj )
	{
		if ( this.Downed && this.Interactable )
		{
			var interplayer = obj.GetComponentInParent<Player>();
			if ( interplayer != null )
			{
				interplayer.CurrentInteraction = this;
				interplayer.ReviveTarget = this;
				interplayer.isReviving = true;
			}
		}
	}


	private void LilDebugThang()
	{
		Log.Info( "thaaang" );
	}

	private void InteractionReviveTriggerExit( GameObject obj )
	{
		var interplayer = obj.GetComponentInParent<Player>();
		if ( interplayer != null )
		{
			interplayer.CurrentInteraction = null;
			interplayer.ReviveTarget = null;
			interplayer.isReviving = false;
		}
	}

	[Rpc.Owner]
	private void CheckAndNudgeStacking()
	{
		if ( PlayerController?.Body == null )
		{
			return;
		}

		// Prevent repeated impulses from stacking into huge launches.
		if ( TimeSinceLastStackNudge < 0.22f )
		{
			return;
		}

		// If we're already moving upward quickly, don't add more vertical force.
		if ( PlayerController.Velocity.z > 120f )
		{
			return;
		}

		// Only consider what is directly under the player, not nearby side contacts.
		var playerFeetPos = GameObject.WorldPosition;
		var traceEnd = playerFeetPos + Vector3.Down * 22f;
		var tr = Scene.Trace.FromTo( playerFeetPos, traceEnd )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit || tr.GameObject == null )
		{
			return;
		}

		// Require an upward-facing surface so side-by-side players don't trigger this.
		if ( tr.Normal.z < 0.6f )
		{
			return;
		}

		var entityBelow = tr.GameObject.GetComponentInParent<Zombie>() as Component
			?? tr.GameObject.GetComponentInParent<Player>() as Component;

		if ( entityBelow == null )
		{
			return;
		}

		// Push away from the player/zombie below to reduce repeated vertical pop.
		var awayFromBelow = (GameObject.WorldPosition - entityBelow.GameObject.WorldPosition).WithZ( 0 );
		Vector3 horizontalVelocity = PlayerController.Velocity.WithZ( 0 ); // ignore vertical
		Vector3 velocityDir;

		if ( awayFromBelow.LengthSquared > 0.001f )
		{
			velocityDir = awayFromBelow.Normal;
		}
		else if ( horizontalVelocity.LengthSquared > 0.001f )
		{
			velocityDir = horizontalVelocity.Normal; // use current motion
		}
		else
		{
			// fallback: use player's facing direction if not moving
			velocityDir = PlayerController.WorldRotation.Forward.WithZ( 0 ).Normal;
		}

		// 2. Add upward bias to lift off
		float verticalBias = .28f;
		Vector3 impulseDir = (velocityDir + new Vector3( 0, 0, verticalBias )).Normal;

		// 3. Scale by desired impulse strength
		float impulseStrength = 90f;
		Vector3 finalImpulse = impulseDir * impulseStrength;

		// 4. Apply to Rigidbody
		var rb = PlayerController.Body;
		if ( rb != null )
		{
			rb.ApplyImpulse( finalImpulse );
		}
		TimeSinceLastStackNudge = 0f;
		PlayerController.PreventGrounding( 0.05f );



		/*if ( !traceResult.Hit )
		{
			Log.Info( "vas" );
			return; // Nothing below us
		}*/

		// Get the entity we're standing on

	}

	protected override void OnUpdate()
	{


		if ( IsProxy ) return;

		UpdateInteractionSelection();
		//DrawInteractionDebug();

		UpdateVoiceState();

		if ( Networking.IsHost )
		{
			if ( !IsTypingChat && Input.Pressed( "dev_debug" ) )
			{
				Log.Info( "hellur" ); var cameraPos = Knife.WorldPosition;
				var direction = playerCamera.WorldRotation.Forward;

				var endPosition = cameraPos + direction * 1000f;
				DebugOverlay.Line( cameraPos, endPosition, Color.Red, 5f );
				var trace = Scene.Trace.FromTo( cameraPos, endPosition ).IgnoreGameObjectHierarchy( this.GameObject ).Run();

				var zom = GameObject.GetPrefab( "zombie.prefab" ).Clone();
				zom.LocalPosition = Vector3.Zero;
				zom.LocalRotation = Rotation.Identity;
				zom.WorldPosition = trace.HitPosition;
				if ( zom == null )
				{
					Log.Info( "uhuh" );
				}

				zom.NetworkSpawn();
			}
		}

		//ControlCamera();

		InteractionUITick();



		/*		//melee

				if ( isKnifing )
				{
					var knifetrace = LineTraceMelee();
					if ( knifetrace != null )
					{


						if ( ObjectsInCurrentKnife.Count == 0 )
						{
							ObjectsInCurrentKnife.Add( knifetrace.Value.GameObject );
						}
						if ( !ObjectsInCurrentKnife.Contains( knifetrace.Value.GameObject ) )
						{
							ObjectsInCurrentKnife.Add( knifetrace.Value.GameObject );

						}
					}
				}*/



		if ( !IsTypingChat && Input.Pressed( "Melee" ) && !isKnifing && !isDeploying )
		{
			PlayerKnife();
		}

		if ( !IsTypingChat && Input.Pressed( "Drop" ) )
		{
			
		}

		if ( isKnifing && CurrentWeaponClass?.WeaponModel != null && Knife != null )
		{
			// Phase 1: briefly lower gun before swapping to knife to reduce popping.
			if ( !KnifeMeshShown && TimeSinceKnifeStarted >= 0.06f )
			{
				CurrentWeaponClass.WeaponModel.RenderOptions.Game = false;
				fpsArms.RenderOptions.Game = false;
				Knife.RenderOptions.Game = true;
				if ( KnifeHand != null )
				{
					KnifeHand.RenderOptions.Game = true;
				}
				KnifeMeshShown = true;
			}

			// Trigger knife attack once after entering melee so anim graphs see a clean edge.
			if ( KnifeMeshShown && !KnifeAttackTriggered && TimeSinceKnifeStarted > 0.08f )
			{
				Knife.Set( "b_attack", true );
				KnifeAttackTriggered = true;
			}

			// Phase 2: bring gun mesh back just before finishing so transition back feels smoother.
			if ( KnifeMeshShown && TimeSinceKnifeStarted >= 0.28f )
			{
				Knife.RenderOptions.Game = false;
				if ( KnifeHand != null )
				{
					KnifeHand.RenderOptions.Game = false;
				}
				CurrentWeaponClass.WeaponModel.RenderOptions.Game = true;
				fpsArms.RenderOptions.Game = true;
			}
		}

		//Log.Info( "is knifin " + isKnifing );
		if ( TimeSinceKnifeStarted >= .25f && isKnifing )
		{
			if ( TimeSinceKnifeStarted >= .34f )
			{
				FinishKnife();
				Log.Info( "Hellllllllo" );

			}

		}


		//deploy check shit | add deploy time to weapon comp; use here


		if ( TimeSinceWeaponSwap >= 0.75f )
		{
			isDeploying = false;
		}









		//holdtype shit
		if ( CurrentWeapon != null )
		{

			switch ( CurrentWeaponClass.WeaponType )
			{
				case BaseWeapon.weaponType.Pistol:
					ManageHoldType( 1 );
					break;
				case BaseWeapon.weaponType.Smg:
					ManageHoldType( 2 );
					break;
				case BaseWeapon.weaponType.Rifle:
					ManageHoldType( 2 );
					break;
				case BaseWeapon.weaponType.Shotgun:
					ManageHoldType( 3 );
					break;
				case BaseWeapon.weaponType.Launcher:
					ManageHoldType( 7 );
					break;
				case BaseWeapon.weaponType.Sniper:
					ManageHoldType( 2 );
					break;
			}
		}





		//aiming

		if ( isAiming )
		{

			var fovaim = playerCamera.FieldOfView.LerpTo( 60f, Time.Delta * 50f );
			if ( CurrentWeaponClass.WeaponType == BaseWeapon.weaponType.Sniper )
			{
				fovaim = playerCamera.FieldOfView.LerpTo( 20f, Time.Delta * 50f );
			}
			playerCamera.FieldOfView = fovaim;
		}

		if ( !isAiming && playerCamera.FieldOfView != Preferences.FieldOfView )
		{
			playerCamera.FieldOfView = playerCamera.FieldOfView.LerpTo( Preferences.FieldOfView, Time.Delta * 20f );
		}

		//health n downs shit

		if ( Health < MaxHealth && TimeSinceDamagedLast >= 5f )
		{
			RegenHealth();
		}

		/*if ( Downed )
		{
			
			
		}*/
		if ( !Downed )
		{
			CheckAndNudgeStacking();
			if ( isSprinting && (isAiming || isFiring) )
			{

				PlayerController.RunSpeed = PlayerController.WalkSpeed;
				isSprinting = false;
			}
			else
			{

				PlayerController.RunSpeed = BaseRunSpeed;
			}

			if ( CurrentWeapon != null )
			{
				ControlMovingAnimationFPS();
				UpdateWeaponSway();
			}


			if ( PlayerController.Velocity.LengthSquared >= 0.2f )
			{
				isWalking = true;
			}
			else
			{
				isWalking = false;
			}

			if ( !IsTypingChat && Input.Down( "run" ) && PlayerController.Velocity.LengthSquared >= 0.2f )
			{
				if ( Downed ) return;
				isSprinting = true;
			}
			else if ( Input.Released( "run" ) || PlayerController.Velocity.LengthSquared <= 0.2f )
			{
				isSprinting = false;
			}
		}


		//movement calcs n shit

		if ( Downed )
		{
			PlayerController.RunSpeed = PlayerController.DuckedSpeed;
			DownedAnimControl();
			return;


		}

		//weapon swapping
		if ( !IsTypingChat && Input.Pressed( "Slot1" ) )
		{
			if ( CurrentWeaponSlot != 0 )
			{
				ChangeCurrentSlot( 0 );
			}

		}

		if ( !IsTypingChat && Input.Pressed( "Slot2" ) )
		{
			if ( Inventory.Weapons.Count >= 2 && CurrentWeaponSlot != 1 )
			{
				ChangeCurrentSlot( 1 );
			}


		}

		if ( !IsTypingChat && Input.Pressed( "Slot3" ) )
		{
			if ( Inventory.Weapons.Count == 3 && CurrentWeaponSlot != 3 )
			{
				ChangeCurrentSlot( 2 );
			}
		}



		//interaction shit

		if ( !IsTypingChat && Input.Pressed( "use" ) && TimeSinceLastInteractionPress >= InteractionPressCooldown && CurrentInteraction != null )
		{
			Log.Info( "vait" );
			TimeSinceLastInteractionPress = 0;
			TimeSinceInteractHeld = 0;
			if ( CurrentInteraction.Hold )
			{
				HeldInteractionTarget = CurrentInteraction;
				HoldingInteraction = true;
				if ( isReviving )
				{
					CurrentInterHoldTime = HasQuickRevive ? 3f : ReviveTime;
				}
				else
				{
					CurrentInterHoldTime = CurrentInteraction.HoldTime;
				}
			}
			else
			{
				CurrentInteraction.OnInteract( this );
			}
		}

		if ( !IsTypingChat && Input.Down( "use" ) )
		{




			if ( HoldingInteraction )
			{
				if ( HeldInteractionTarget == null || !IsValidInteractionCandidate( HeldInteractionTarget, out _ ) )
				{
					HoldingInteraction = false;
					HeldInteractionTarget = null;
					TimeSinceInteractHeld = 0;
				}

				if ( HoldingInteraction && TimeSinceInteractHeld >= CurrentInterHoldTime )
				{
					Log.Info( "sending interact" );
					if ( HeldInteractionTarget != null )
					{
						HeldInteractionTarget.OnInteract( this );
					}
					TimeSinceInteractHeld = 0;

					if ( isReviving )
					{
						HoldingInteraction = false;
						isReviving = false;
						CurrentInteraction = null;
					}


				}
			}




		}

		if ( !IsTypingChat && Input.Released( "use" ) )
		{
			TimeSinceInteractHeld = 0;
			HoldingInteraction = false;
			HeldInteractionTarget = null;
			isReviving = false;

		}


	}
}
