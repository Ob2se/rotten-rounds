using Sandbox;

public sealed class MainMenuCharDresser : Component
{

	ClothingContainer clothingContainer = ClothingContainer.CreateFromLocalUser();

	[Property]
	SkinnedModelRenderer CharRenderer { get; set; }

	protected override void OnStart()
	{
		clothingContainer.Apply( CharRenderer );
	}



	protected override void OnUpdate()
	{

	}
}
