using Sandbox;
using System;

public sealed class RandomThunder : Component
{

	[Property]
	private SoundPointComponent SoundPoint { get; set; }

	private Random rand = new Random();

	private TimeSince TimeSinceLastThunder;

	private float NextThunderTime;


	private void GetRandomTime()
	{
		NextThunderTime = rand.Next(20, 90);
		
	}

	private void PlayThunderSound()
	{
		if (SoundPoint != null)
		{
			SoundPoint.StartSound();
		}
		TimeSinceLastThunder = 0;
		GetRandomTime();
	}	

	protected override void OnUpdate()
	{
		if(TimeSinceLastThunder >= NextThunderTime)
		{
			PlayThunderSound();
		}
	}
}
