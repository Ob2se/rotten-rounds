using Sandbox;

public struct ChatMessageStruct( string steamId, string playerName, string message )
{
	public string SteamId { get; set; } = steamId;
	public string PlayerName { get; set; } = playerName;
	public string Message { get; set; } = message;
}
