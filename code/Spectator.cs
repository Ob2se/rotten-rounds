using Sandbox;
using System;

public sealed class Spectator : Component
{

	[RequireComponent]
	public CameraComponent Camera { get; set; }

	public List<Player> Players { get; set; } = new();

	public int CurrentPlayerIndex { get; set; } = 0;

	public string NameOfPlayer { get; set; }

	public Player CurrentSpectatedPlayer { get; set; }


	[Property]
	public ChatHud ChatHud { get; set; }


	protected override void OnStart()
	{
		base.OnStart();

		if ( IsProxy )
		{
			Camera.Enabled = false;
			ChatHud.Enabled = false;
			return;
		}


		foreach ( var player in Scene.GetAllComponents<Player>() )
		{
			Players.Add( player );
			
		}
		GameModeManager.PlayerListChange -= PlayerListChanged;

		StartSpectating( Players.FirstOrDefault() );

	}


	protected override void OnDestroy()
	{
		base.OnDestroy();
		if ( IsProxy ) return;

		if ( CurrentSpectatedPlayer != null )
		{
			CurrentSpectatedPlayer.NameTag.Enabled = true;
		}
	}

	[Rpc.Owner]
	public void StartSpectating( Player player )
	{
		if ( player == null || !player.IsValid() ) return;

		var spectatorPoint = player.SpectatorPoint;

		if ( spectatorPoint == null || !spectatorPoint.IsValid() ) return;

		if ( spectatorPoint.Scene != GameObject.Scene )
		{
			Log.Warning( "SpectatorPoint is in a different scene, cannot parent." );
			return;
		}

		if(CurrentSpectatedPlayer != null)
		{
			CurrentSpectatedPlayer.NameTag.Enabled = true;
		}

		CurrentSpectatedPlayer = player;
		GameObject.Parent = spectatorPoint;
		Camera.LocalPosition = Vector3.Zero;
		Camera.LocalRotation = Rotation.Identity;
		Camera.LocalScale = Vector3.One;
		NameOfPlayer = player.Network?.Owner?.DisplayName ?? "Unknown";
		player.NameTag.Enabled = false;
	}


	[Rpc.Owner]
	public void PlayerListChanged()
	{
		Players.Clear();
		foreach ( var player in Scene.GetAllComponents<Player>() )
		{
			Players.Add( player );
		}
	}


	[Rpc.Owner]
	public void SpectateNext()
	{
		if ( Players == null || Players.Count == 0 ) return;

		int nextIndex = (CurrentPlayerIndex + 1) % Players.Count;
		var nextSpec = Players[nextIndex];

		if ( nextSpec == null ) return;

		CurrentPlayerIndex = nextIndex;
		//Log.Info( "trying to spectate " + nextSpec.Network?.Owner?.DisplayName ?? "Unknown" );
		StartSpectating( nextSpec );
	}

	[Rpc.Owner]
	public void SpectatePrevious()
	{
		if ( Players == null || Players.Count == 0 ) return;

		int nextIndex = (CurrentPlayerIndex - 1 + Players.Count) % Players.Count;
		var nextSpec = Players[nextIndex];

		if ( nextSpec == null ) return;

		CurrentPlayerIndex = nextIndex;
		//Log.Info( "trying to spectate " + nextSpec.Network?.Owner?.DisplayName ?? "Unknown" );
		StartSpectating( nextSpec );
	}


	protected override void OnUpdate()
	{

		if ( IsProxy ) return;


		if ( Input.Pressed( "attack1" ) )
		{
			Log.Info( "hellloooo" );
			SpectatePrevious();
			
		}


		if ( Input.Pressed( "attack2" ) )
		{
			SpectateNext();
		}


	}
}
