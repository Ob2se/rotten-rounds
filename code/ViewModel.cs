using Sandbox;

public sealed class ViewModel : Component
{

	

	string ModelPath = "models/fpsarms/first_person_arms_citizen_4fingers2.vmdl";

	[Rpc.Owner]
	protected override void OnStart()
	{

		//get needed components/users customization
		PlayerController playerController = GameObject.Components.Get<PlayerController>();

		CameraComponent playerCamera = Scene.Camera.GetComponent<CameraComponent>();

		var clothingContainer = ClothingContainer.CreateFromLocalUser();
		
		
		//needs to be removed
		clothingContainer.PrefersHuman = true;

		if ( clothingContainer.PrefersHuman )
		{
			ModelPath = "models/fpsarms/first_person_arms2.vmdl";
		}

		//create container for arms so we can control transform
		var fpsArmsContainer = new GameObject();

		//add the arms to the container
		var fpsarms = fpsArmsContainer.AddComponent<SkinnedModelRenderer>(true);

		//parent the container to the camera
		fpsArmsContainer.SetParent( playerCamera.GameObject );
		
		//zero out transform
		fpsArmsContainer.LocalPosition = Vector3.Zero;
		fpsArmsContainer.LocalRotation = Rotation.Identity;
		fpsArmsContainer.LocalScale = Vector3.One;

		//add offset
		fpsArmsContainer.LocalPosition = new Vector3 ( 0, 0, -60 );

		//load and set arms model
		Model fpsModel = Model.Load( ModelPath );
		fpsarms.Model = fpsModel;


	}




	protected override void OnUpdate()
	{
		
	}
}
