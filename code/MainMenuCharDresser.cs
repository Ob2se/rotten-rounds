using Sandbox;
using System.Threading.Tasks;

public sealed class MainMenuCharDresser : Component
{

	[Property]
	public Dresser charDresser { get; set; }

	[Property]
	SkinnedModelRenderer CharRenderer { get; set; }

	


	protected override void OnStart()
	{
		_ = ApplyClothesIGuess();
	}

	[Rpc.Broadcast]
	public void BroadcastSetClothing()
	{
		_ = ApplyClothesIGuess();
	}

	
	public async Task ApplyClothesIGuess()
	{
		await charDresser.Apply();
	}

	protected override void OnUpdate()
	{

	}
}
