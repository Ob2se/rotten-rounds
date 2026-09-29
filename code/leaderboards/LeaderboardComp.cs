using Sandbox;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class LeaderboardComp : Component
{


	public async Task<List<Sandbox.Services.Leaderboards.Board2.Entry>> GetLeaderboardDataTop3(string stat)
	{
		var board = Sandbox.Services.Leaderboards.GetFromStat( "clickhq.rottenrounds", stat );
		board.MaxEntries = 3;
		board.SetAggregationMax();
		await board.Refresh();

		return board.Entries.ToList();

	}


	public async Task<List<Sandbox.Services.Leaderboards.Board2.Entry>> GetLeaderboardScoreTop3()
	{
		var board = Sandbox.Services.Leaderboards.GetFromStat( "clickhq.rottenrounds", "score" );
		board.MaxEntries = 3;
		board.SetAggregationMax();

		await board.Refresh();

		return board.Entries.ToList();
	}

	public async Task<Sandbox.Services.Leaderboards.Board2.Entry> GetLeaderboardDataPersonal( string stat )
	{
		var board = Sandbox.Services.Leaderboards.GetFromStat( "clickhq.rottenrounds", stat );
		board.TargetSteamId = Connection.Local.SteamId;
		board.SetAggregationMax();
		await board.Refresh();

		return board.Entries.First();

	}


	public async Task<Sandbox.Services.Leaderboards.Board2.Entry> GetLeaderboardScorePersonal()
	{
		var board = Sandbox.Services.Leaderboards.GetFromStat( "clickhq.rottenrounds", "score" );
		
		board.CenterOnSteamId( Connection.Local.SteamId );
		board.SetAggregationMax();
		await board.Refresh();
		
		foreach(var x in board.Entries)
		{
			if(x.SteamId == Connection.Local.SteamId)
			{
				return x;
			}
		}
		return board.Entries.First();
	}



	private void CalculateScore()
	{
		/*var hsR = zHead.Value / zkills.Value;
		var hRound = HighestRound.Value;
		var kills = zkills.Value;
		var maxScore = 1000000;
		var score = (Math.Pow( hsR, 2 ) * (double)hRound * (double)kills) + ((double)kills * 0.0001);*/
	}



	protected override void OnUpdate()
	{

	}
}
