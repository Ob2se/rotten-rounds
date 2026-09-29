using Sandbox;

public sealed class CustomMapSettings : Component
{
	[Property]
	public bool UseCustomZombieModel { get; set; }

	[Property]
	public bool UseZombieInfluenceZones { get; set; }

	[Property]
	public bool CustomPlayerModels { get; set; }



	protected override void OnUpdate()
	{

	}
}
