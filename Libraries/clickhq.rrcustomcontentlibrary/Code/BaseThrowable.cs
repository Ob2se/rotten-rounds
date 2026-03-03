using Sandbox;

public sealed class BaseThrowable : Component
{
	[Property]
	public string ThrowableName { get; set; } = "BaseThrowable";

	[Property, ShowIf("Lethal", true)]
	public int Damage { get; set; } = 100;

	[Property]
	public bool Lethal { get; set; } = true;

	[Property]
	public ThrowableType TypeOfThrowable { get; set; }

	[Property]
	public PrefabFile ExplosionEffect { get; set; }


	[Property]
	public SoundFile ExplosionSound { get; set; }

	[Property]
	public SkinnedModelRenderer ThrowableModel { get; set; }

	[Property]
	public int MaxAmount { get; set; } = 4;

	public enum ThrowableType
	{
		Grenade,
		Tatical
	}


	protected override void OnUpdate()
	{

	}
}
