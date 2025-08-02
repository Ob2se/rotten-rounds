using Sandbox;
using Sandbox.Citizen;
using System.Reflection;
using System.Runtime.InteropServices;


public sealed class CharacterSetup : Component
{
	[RequireComponent]
	private PlayerController playerController { get; set; }

	private CameraComponent playerCamera { get; set; }

	ClothingContainer clothingContainer = ClothingContainer.CreateFromLocalUser();

	static public GameObject fpsArmsContainer;

	static public SkinnedModelRenderer fpsarms = new SkinnedModelRenderer();

	[Rpc.Owner]
	private void CreateViewModel()
	{
		string ModelPath = "models/first_person/first_person_arms_citizen_4fingers.vmdl";

		//get camera
		playerCamera = Scene.Camera;

		playerCamera.ZNear = 5;



	

		//needs to be removed
		clothingContainer.PrefersHuman = true;

		if ( clothingContainer.PrefersHuman )
		{
			ModelPath = "models/first_person/first_person_arms.vmdl";
		}

		//create container for arms so we can control transform
		var fpsArmsContainer = new GameObject();

		//add the arms to the container
		fpsarms = fpsArmsContainer.AddComponent<SkinnedModelRenderer>( true );

		//parent the container to the camera
		fpsArmsContainer.SetParent( playerCamera.GameObject );

		//zero out transform
		fpsArmsContainer.LocalPosition = Vector3.Zero;
		fpsArmsContainer.LocalRotation = Rotation.Identity;
		fpsArmsContainer.LocalScale = Vector3.One;

		//add offset
		//fpsArmsContainer.LocalPosition = new Vector3( 5, 0, 0 );

		//load and set arms model
		Model fpsModel = Model.Load( ModelPath );
		fpsarms.Model = fpsModel;

		AnimationGraph animG = AnimationGraph.Load( "models/weapons/sbox_pistol_usp/v_usp.vanmgrph" );
		fpsarms.AnimationGraph = animG;
		

	}






	[Rpc.Owner]
	public void SetVMVisibility(bool visibility)
	{ 
		fpsarms.Enabled = visibility;
		
	}




	protected override void OnStart()
	{

		if ( IsProxy ) return;
		base.OnStart();

		//create citizen and clothing
		CreatePlayerRenderer();


		//create and hide viewmodel
		CreateViewModel();

	}

	[Rpc.Broadcast]
	private void CreatePlayerRenderer()
	{
		playerController.CreateBodyRenderer();
		clothingContainer.Apply( playerController.Renderer );
	}


	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;


		base.OnFixedUpdate();

	}

	protected override void OnUpdate()
	{

		if ( IsProxy ) return;

		base.OnUpdate();

		

		if ( playerController.ThirdPerson ) 
		{
			SetVMVisibility( false );
		}
		else
		{
			SetVMVisibility( true );
		}
	}
}
