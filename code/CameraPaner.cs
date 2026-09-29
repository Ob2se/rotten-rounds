using Sandbox;
using System;
public sealed class CameraPanner : Component
{
	[Property]
	public CameraComponent CameraPlay { get; set; }
	[Property]
	public CameraComponent CameraLeaderboards { get; set; }
	[Property]
	public CameraComponent CameraCustomize { get; set; }
	[Property]
	public CameraComponent CameraCreate { get; set; }
	[Property]
	public CameraComponent CameraJoin { get; set; }
	public Vector3 BaseCameraPoint;
	public Rotation BaseCameraRotation;
	public Vector3 TargetPos;
	public Rotation TargetRot;
	private float t = 0f;
	private bool panningCamera = false;
	Vector3 panStartPos;
	Rotation panStartRot;
	protected override void OnStart()
	{
		BaseCameraPoint = this.GameObject.WorldPosition;
		BaseCameraRotation = this.GameObject.WorldRotation;
	}
	public void PanCamera( CameraComponent camera )
	{
		if ( camera != null )
		{
			panStartPos = this.GameObject.WorldPosition;
			panStartRot = this.GameObject.WorldRotation;
			TargetPos = camera.GameObject.WorldPosition;
			TargetRot = camera.GameObject.WorldRotation;
			t = 0f;
			panningCamera = true;
		}
	}
	public void ResetCamera()
	{
		panStartPos = this.GameObject.WorldPosition;
		panStartRot = this.GameObject.WorldRotation;
		TargetPos = BaseCameraPoint;
		TargetRot = BaseCameraRotation;
		t = 0f;
		panningCamera = true;
	}
	private float SmoothStep( float t )
	{
		return t * t * (3 - 2 * t);
	}
	protected override void OnUpdate()
	{
		if ( panningCamera )
		{
			float duration = 1f;
			t += Time.Delta / duration;
			t = Math.Clamp( t, 0f, 1f );
			float easedT = SmoothStep( t );
			this.GameObject.WorldPosition = Vector3.Lerp( panStartPos, TargetPos, easedT );
			this.GameObject.WorldRotation = Rotation.Slerp( panStartRot, TargetRot, easedT );
			if ( t >= 1f )
			{
				panningCamera = false;
			}
		}
	}
}
