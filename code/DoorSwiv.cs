using Sandbox;

public sealed class DoorSwiv : Component
{

	public bool isOpen = false;

	public bool isHovered = false;

	[Property]
	private ModelRenderer DoorModel { get; set; }

	protected override void OnStart()
	{
		
	}

	protected override void OnUpdate()
	{
		var angles = DoorModel.WorldRotation.Angles();

		float targetYaw = (!isOpen && isHovered) ? 70f : 0f;
		float speed = isHovered ? 50f : 5f;

		float t = MathX.Clamp( Time.Delta * speed, 0f, 1f );
		angles.yaw = MathX.Lerp( angles.yaw, targetYaw, t );

		DoorModel.WorldRotation = Rotation.From( angles );

		if ( DoorModel.WorldRotation.y >= 70 )
		{
			isOpen = true;
		}

		if ( DoorModel.WorldRotation.y <= 0 )
		{
			isOpen = false;
		}

	}
}
