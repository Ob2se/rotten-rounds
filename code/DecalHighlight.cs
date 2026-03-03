using Sandbox;

public sealed class DecalHighlight : Component
{
	[Property] public Decal DecalMainHighlight { get; set; }
	[Property] public Decal DecalSelectedHighlight { get; set; }


	public bool Highlighted = false;

	public void HighlightOn()
	{
		Highlighted = true;
	}

	public void HighlightOff()
	{
		Highlighted = false;
	}

	protected override void OnUpdate()
	{
		if ( Highlighted )
		{
			DecalMainHighlight.Enabled = false;
			DecalSelectedHighlight.Enabled = true;
		}
		else
		{
			DecalMainHighlight.Enabled = true;
			DecalSelectedHighlight.Enabled = false;
		}
	}	
}

