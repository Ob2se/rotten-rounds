using Sandbox;

public sealed class LightFlicker : Component
{

	[Property]
	PointLight LightSource { get; set; }


	//not really flicker but like a throb effect or whatever
	[Property]
	float FlickerSpeed { get; set; } = 1.0f;


	//how strong the light change is
	[Property]
	float Intensity { get; set; } = 1.0f;

	TimeSince TimeSinceFlicker { get; set; }

	private bool Inner { get; set; } = true;
	private bool Outter { get; set; } = false;

	float LastAttenuation { get; set; }



	//fade in
	private void In()
	{
		LightSource.Attenuation = MathX.LerpTo(LightSource.Attenuation, LastAttenuation - Intensity, Time.Delta * FlickerSpeed );
	}

	//fade out
	private void Out()
	{
		LightSource.Attenuation = MathX.LerpTo( LightSource.Attenuation, LastAttenuation + Intensity, Time.Delta * FlickerSpeed );
	}


	protected override void OnUpdate()
	{
		if ( TimeSinceFlicker >= FlickerSpeed && Inner )
		{
			Inner = false;
			Outter = true;
			TimeSinceFlicker = 0;
		}
		if ( TimeSinceFlicker >= FlickerSpeed && Outter )
		{
			Outter = false;
			Inner = true;
			TimeSinceFlicker = 0;
		}
		if ( Inner )
		{
			LastAttenuation = LightSource.Attenuation;
			In();
		}
		if ( Outter )
		{
			LastAttenuation = LightSource.Attenuation;
			Out();
		}
	}
}
