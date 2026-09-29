using Sandbox;
using Sandbox.Network;
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using static Sandbox.VideoWriter;

public sealed class MainMenuManager : Component
{




	public async Task CreateLobby(LobbyConfig config)
	{
		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( config );

			await Task.DelayRealtimeSeconds( 1 );
			
			//SceneFile lobbyScene;

			//ResourceLibrary.TryGet<SceneFile>( "scenes/lobby.scene", out lobbyScene);
			Scene.LoadFromFile( "scenes/lobby.scene" );
		}
	}

	public async Task GetStats()
	{
		var stats = Sandbox.Services.Stats.GetLocalPlayerStats( "clickhq.rottenrounds" );

		await stats.Refresh();

		var zkills = stats.Get( "zombie_kills" );
		var zHead = stats.Get( "zombie_headshot_kill" );
		var HighestRound = stats.Get( "highest_round" );

/*		if ( zkills.Value == 0 )
		{
			Sandbox.Services.Stats.SetValue( "zombie_kills", 0 );
		}
		if ( zHead.Value == 0 )
		{
			Sandbox.Services.Stats.SetValue( "zombie_headshot_kills", 0 );
		}
		if ( HighestRound.Max == 0 )
		{
			Sandbox.Services.Stats.SetValue( "highest_round", 0 );
		}
*/
		var rank = stats.Get( "rank" );
		Log.Info("rank: " + rank.Max);
		if ( rank.Value == 0 )
		{
			Sandbox.Services.Stats.SetValue( "rank", 1 );
			return;
		}



		//1-5 bronze 6-10 silver 11-15 gold 16-20 diamond 21-25 ruby
		if(rank.Max > 0 )
		{

			var hsR = zHead.Value / zkills.Value;
			var hRound = HighestRound.Value;
			var kills = zkills.Value;

			var hsBonus = 1 + (hsR * 0.5);
			var roundScore = Math.Pow( (double)hRound, 0.8 );
			var score = (roundScore * (double)kills * hsBonus) + ((double)kills * 0.0001);

			Sandbox.Services.Stats.SetValue( "score", score );

			var rankCeiling = 500000;
			var rankcalc = Math.Clamp( Math.Pow( score / rankCeiling, 0.55 ) * 25, 1, 25 );

			if( Math.Floor( rankcalc ) > rank.Max)
			Sandbox.Services.Stats.SetValue( "rank", Math.Floor(rankcalc) );

		}



	}


	protected override void OnStart()
	{
		base.OnStart();


		_ = GetStats();
		

	}


	protected override void OnUpdate()
	{

		/*if ( Input.Pressed( "Reload" ) )
		{

			var config = new LobbyConfig()
			{
				MaxPlayers = 1,
				Privacy = LobbyPrivacy.FriendsOnly,
				Name = "Rotten Rounds | Waiting to start... "
			};
			Networking.CreateLobby( config );
			Scene.LoadFromFile( "scenes/whatthefuck.scene" );
		}*/


	}
}
