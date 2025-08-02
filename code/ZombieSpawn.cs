using Sandbox;
using System;

public sealed class ZombieSpawn : Component
{

	[Property]
	public Window AssociatedWindow { get; set; }

	[Property]
	public bool activated;

	[Property] public string SpawnID { get; set; } = Guid.NewGuid().ToString();


}
