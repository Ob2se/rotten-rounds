using Sandbox;
using Sandbox.Network;
using System.Threading.Tasks;

public sealed class MainMenuManager : Component
{




	public async Task CreateLobby(LobbyConfig config)
	{
		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( config );

			await Task.DelayRealtimeSeconds( 1 );
			
			SceneFile lobbyScene;

			ResourceLibrary.TryGet<SceneFile>( "scenes/lobby.scene", out lobbyScene);
			Scene.Load( lobbyScene );
		}
	}




	protected override void OnStart()
	{
		base.OnStart();



	}


	protected override void OnUpdate()
	{

	}
}
