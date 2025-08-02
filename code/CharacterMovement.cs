using Sandbox;
using System.Diagnostics;

public sealed class CharacterMovement : Component
{
	Vector3 Velocity;
	float maxSpeed;

	[Property]
	public PhysicsBody PhysicsBody { get; set; }

	

	protected override void OnStart()
	{
		base.OnStart();

		

	}


	protected override void OnUpdate()
	{
		

	}

	//public bool isGrounded()
	//{
	//	Scene.Trace.FromTo(GameObject)
	//}


	



	private Vector3 GetWishDirection()
	{
		Vector3 forward = WorldRotation.Forward.WithZ( 0 ).Normal;
		Vector3 right = WorldRotation.Right.WithZ( 0 ).Normal;

		return (forward * (Input.Down( "forward" ) ? 1 : 0) +
								 right * (Input.Down( "right" ) ? 1 : 0) -
								 right * (Input.Down( "left" ) ? 1 : 0) +
								 forward * (Input.Down( "backward" ) ? -1 : 0)).Normal;

	}


	private Vector3 GetVelocity(Vector3 wishDirection)
	{
		return wishDirection * maxSpeed;
	}


}
