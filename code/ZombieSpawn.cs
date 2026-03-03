using Sandbox;
using System;

public sealed class ZombieSpawn : Component
{

	[Property]
	public Window AssociatedWindow { get; set; }

	[Property]
	public bool activated;

	[Property]
	public bool RequireAWindow { get; set; } = true;

	protected override void DrawGizmos()
	{
		if ( !Scene.Editor.Selection.Contains( this.GameObject ) ) return;

		if ( AssociatedWindow == null ) return;
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.Line(
			GameObject.Transform.World.Position,
			AssociatedWindow.GameObject.Transform.World.Position
		);
	}


}
