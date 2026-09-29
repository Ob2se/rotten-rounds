using Sandbox;
using System;
using System.Diagnostics.Contracts;

public sealed class Teleporter : Component, IPower, IInteraction
{
	public float HoldTime { get; set; } = 0f;

	public bool Interactable { get; set; } = true;

	public bool Hold { get; set; } = false;

	public GameObject GO => this.GameObject;

	[Sync]
	public bool Powered { get; set; } = false;

	[Sync]
	public bool TeleporterActivated { get; set; } = false;

	[Sync]
	public bool TeleporterActive { get; set; } = false;

	[Property, ShowIf("RequireLink", true)]
	public TeleporterLink TargetLink { get; set; }

	[Property, ShowIf( "FreeTeleport", true )]
	public GameObject TeleportTarget { get; set; }


	[Property]
	public float CoolDown { get; set; } = 10f;

	[Property]
	public SphereCollider TeleportTrigger { get; set; }

	[Sync]
	public List<Player> PlayersInTrigger { get; set; } = new List<Player>();


	[Sync]
	public bool OnCooldown { get; set; } = false;


	TimeSince TimeSinceCoolDownStarted { get; set; }

	[Property]
	public bool RequireLink { get; set; } = false;

	[Sync]
	public bool LinkStarted { get; set; } = false;

	[Property]
	public bool FreeTeleport { get; set; } = false;

	[Property, ShowIf( "FreeTeleport", true )]
	public bool TempTeleport { get; set; } = false;


	[Property, ShowIf( "TempTeleport", true )]
	public float TempTeleportDuration { get; set; } = 30f;

	[Property]
	public bool TeleportHasCost { get; set; } = false;

	[Property, ShowIf( "TeleportHasCost", true )]
	public int TeleportCost { get; set; } = 500;

	[Sync]
	public bool TempTeleportStarted { get; set; } = false;

	private TimeSince TimeSinceTempTeleport { get; set; }

	[Sync]
	public List<Player> PlayersTempTeleport { get; set; } = new List<Player>();


	public void OnInteract( Player player )
	{
		if ( !Powered )
		{
			OnInteractionFailed( player, IInteraction.InteractionFReason.NoPower );
			return;
		}

		if ( RequireLink && TargetLink == null )
		{

			return;
		}

		if ( RequireLink && TargetLink.LinkStarted )
		{
			return;
		}

		if ( RequireLink && !TeleporterActivated )
		{
			TargetLink.LinkTeleporter( this );
			StartLink();
			return;
		}

		if ( TeleporterActivated && TeleporterActive && !TempTeleport)
		{
			Log.Info( "hello teleport" );
			TeleportPlayers();
			return;
		}
		if ( TeleporterActivated && TeleporterActive && TempTeleport )
		{
			Log.Info( "hello teleport temp" );
			TeleportPlayersTemp();
		}

	}

	[Rpc.Host]
	private void CoolDownStarted()
	{
		TimeSinceCoolDownStarted = 0f;
		OnCooldown = true;
		TeleporterActive = false;
	}

	[Rpc.Host]
	private void CoolDownEnded()
	{
		TeleporterActive = true;
		OnCooldown = false;
	}



	[Rpc.Host]
	private void TeleportPlayers()
	{
		Log.Info( PlayersInTrigger.Count() );
		var list = PlayersInTrigger;
		var totalPlayers = list.Count;
		for ( int i = 0; i < totalPlayers; i++ )
		{
			var player = list[i];

			if ( player != null )
			{
				Log.Info( "what the freak" );
				MovePlayer( i, totalPlayers );
				
			}
		}
		CoolDownStarted();
	}

	[Rpc.Host]
	private void TeleportPlayersTemp()
	{
		PlayersTempTeleport.Clear();
		var list = PlayersInTrigger;
		var totalPlayers = list.Count;
		for ( int i = 0; i < totalPlayers; i++ )
		{
			var player = list[i];
			if ( player == null ) continue;

			MovePlayer( i, totalPlayers );
			PlayersTempTeleport.Add( player );
		}
		//PlayersTempTeleport = list;
		TimeSinceTempTeleport = 0f;
		TempTeleportStarted = true;
		CoolDownStarted();
	}



	[Rpc.Host]
	private void EndTempTeleport()
	{
		var list = PlayersTempTeleport;
		var totalPlayers = list.Count;
		for ( int i = 0; i < totalPlayers; i++ )
		{
			var player = list[i];
			Log.Info( player.ToString() );
			if ( player != null )
			{
				Log.Info( "returning player" );
				ReturnPlayer( i, totalPlayers );
			}
		}
		PlayersTempTeleport = new List<Player>();
		TempTeleportStarted = false;
	}


	[Rpc.Broadcast]
	private void ReturnPlayer( int player, int totalPlayers )
	{
		var playertoreturn = PlayersTempTeleport[player];
		if ( playertoreturn != null )
		{
			playertoreturn.GameObject.WorldPosition = GetSpacedReturnPosition( player, totalPlayers );
		}
	}


	[Rpc.Broadcast]
	private void MovePlayer( int player, int totalPlayers )
	{
		var playertoteleport = PlayersInTrigger[player];
		if ( playertoteleport != null )
		{
			playertoteleport.GameObject.WorldPosition = GetSpacedTeleportPosition( player, totalPlayers );
		}
	}

	private Vector3 GetTeleportDestinationCenter()
	{
		if ( FreeTeleport )
		{
			return TeleportTarget != null ? TeleportTarget.WorldPosition : GameObject.WorldPosition;
		}

		return TargetLink != null ? TargetLink.WorldPosition : GameObject.WorldPosition;
	}

	private Vector3 GetSpacedTeleportPosition( int index, int totalPlayers )
	{
		var center = GetTeleportDestinationCenter();
		var baseHeightOffset = Vector3.Up * 4f;

		if ( totalPlayers <= 1 )
		{
			return ResolveGroundedPosition( center + baseHeightOffset, center );
		}

		const float spacingRadius = 48f;
		int playersInRing = 6;
		int ring = 0;
		int ringIndex = index;

		while ( ringIndex >= playersInRing )
		{
			ringIndex -= playersInRing;
			ring++;
			playersInRing += 6;
		}

		float radius = spacingRadius * (ring + 1);
		float angleStep = (MathF.PI * 2f) / playersInRing;
		float angle = angleStep * ringIndex;
		var horizontalOffset = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * radius;

		var desired = center + horizontalOffset + baseHeightOffset;
		return ResolveGroundedPosition( desired, center );
	}

	private Vector3 GetSpacedReturnPosition( int index, int totalPlayers )
	{
		var center = this.GameObject.WorldPosition;
		var baseHeightOffset = Vector3.Up * 4f;

		if ( totalPlayers <= 1 )
		{
			return ResolveGroundedPosition( center + baseHeightOffset, center );
		}

		const float spacingRadius = 48f;
		int playersInRing = 6;
		int ring = 0;
		int ringIndex = index;

		while ( ringIndex >= playersInRing )
		{
			ringIndex -= playersInRing;
			ring++;
			playersInRing += 6;
		}

		float radius = spacingRadius * (ring + 1);
		float angleStep = (MathF.PI * 2f) / playersInRing;
		float angle = angleStep * ringIndex;
		var horizontalOffset = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * radius;

		var desired = center + horizontalOffset + baseHeightOffset;
		return ResolveGroundedPosition( desired, center );
	}

	private Vector3 ResolveGroundedPosition( Vector3 desired, Vector3 center )
	{
		if ( TryGetGroundedPlacement( desired, out var grounded ) )
		{
			return grounded;
		}

		// Search nearby slots if the chosen position has no walkable ground below.
		for ( int ring = 1; ring <= 3; ring++ )
		{
			float radius = 24f * ring;
			for ( int i = 0; i < 8; i++ )
			{
				float angle = (MathF.PI * 2f / 8f) * i;
				var offset = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f ) * radius;
				var candidate = desired + offset;
				if ( TryGetGroundedPlacement( candidate, out grounded ) )
				{
					return grounded;
				}
			}
		}

		if ( TryGetGroundedPlacement( center, out grounded ) )
		{
			return grounded;
		}

		return desired;
	}

	private bool TryGetGroundedPlacement( Vector3 position, out Vector3 groundedPosition )
	{
		var start = position + Vector3.Up * 80f;
		var end = position + Vector3.Down * 400f;
		var trace = Scene.Trace
			.FromTo( start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !trace.Hit || trace.GameObject == null || trace.Normal.z < 0.45f )
		{
			groundedPosition = default;
			return false;
		}

		groundedPosition = trace.HitPosition + Vector3.Up * 6f;
		return true;
	}


	[Rpc.Host]
	private void StartLink()
	{
		LinkStarted = true;
	}


	public void OnInteractionFailed( Player player, IInteraction.InteractionFReason reason )
	{
		
	}

	[Rpc.Host]
	public void TeleporterActivate()
	{
		LinkStarted = false;
		TeleporterActivated = true;
		TeleporterActive = true;
	}


	
	public void OnPowerTurnedOn()
	{
		TurnOnPower();
	}

	[Rpc.Host]
	public void TurnOnPower()
	{
		Powered = true;
	}


	public void TeleportTriggerEnter( GameObject obj )
	{
		AddPlayerTolist( obj );
	}

	public void TeleportTriggerExit( GameObject obj )
	{
		RemovePlayerFromList( obj );
	}

	[Rpc.Host]
	private void RemovePlayerFromList(GameObject obj)
	{
		var player = obj.GetComponentInParent<Player>();
		var list = PlayersInTrigger;
		if ( player != null )
		{

			if ( list.Contains( player ) )
			{
				list.Remove( player );

			}
		}

		PlayersInTrigger = list;

	}
	


	[Rpc.Host]
	private void AddPlayerTolist(GameObject obj)
	{
		var player = obj.GetComponentInParent<Player>();

		var list = PlayersInTrigger;

		if ( player != null )
		{
			if ( !list.Contains( player ) )
			{
				list.Add( player );
			}
		}
	}

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{ 
			TeleportTrigger.OnObjectTriggerEnter += TeleportTriggerEnter;
			TeleportTrigger.OnObjectTriggerExit += TeleportTriggerExit;
			if ( !RequireLink )
			{
				TeleporterActivate();
			}
		}

		var gamemode = Scene.GetAllComponents<GameModeManager>().FirstOrDefault();

		if ( gamemode.PowerOn )
		{
			
		}
	}


	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( TempTeleportStarted )
		{
			//Log.Info( "temp teleport started" );
			if ( TimeSinceTempTeleport >= TempTeleportDuration )
			{
				//Log.Info("end temp teleport");
				foreach ( var player in PlayersTempTeleport )
				{
					if ( player.Upgrading )
					{
						return;
					}
				}
				EndTempTeleport();
			}
		}

		if ( OnCooldown )
		{
			if ( TimeSinceCoolDownStarted >= CoolDown )
			{
				CoolDownEnded();
			}
		}
	}
}
