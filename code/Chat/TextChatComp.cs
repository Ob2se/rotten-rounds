using Sandbox;
using System;
using static ChatMessageStruct;

public sealed class TextChatComp : Component
{
	public static event Action<ChatMessageStruct> ChatMessageReceived;


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
		ChatMessageReceived?.Invoke( new ChatMessageStruct( steamId, playerName, message ) );
	}



	protected override void OnUpdate()
	{

	}
}
