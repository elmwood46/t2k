using Godot;
using System;

public partial class CameraManager : Node3D
{
	[Export] public float RotationDuration { get; set; } = 0.5f; // Duration to complete the rotation
	[Export] public int RotationSubdivisions { get; set; } = 4; // number of fixed _camera angles allowed
	[Export] public float RotationOffset { get; set; } = 30f; // rotation offset of the gimbal, in degrees
	[Export] public float CameraZoomMin { get; set; } = 12f;
	[Export] public float CameraZoomMax { get; set; } = 20f;
	[Export] public Camera3D Camera;
	public static CameraManager Instance { get; private set; }

	private MouseVelocityTracker _mouseVTracker;
	private Prop _targetObj;
	private int _currSubdivision = 0;  // track the current fixed _camera angle
	private bool _rotating = false; // flag for animating _camera rotation
	private float _targetRotation = 0f; // Target rotation in radians
	private float _initialRotation; // Starting rotation in radians
	private float _rotationStartTime; // Time when rotation started
	private float _rotationAngle; // the angle which the _camera moves every time you press "R"

	public override void _Ready()
	{

		Rotation = new Vector3(Rotation.X, Mathf.DegToRad(RotationOffset), Rotation.Z);

		if (Engine.IsEditorHint()) return;

		Instance = this;
		_targetObj = Player.Instance;
		_mouseVTracker = new MouseVelocityTracker(GetViewport());

		_rotationAngle = 360f / (float)RotationSubdivisions;
	}

	// process - rotate and zoom the _camera 
	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint()) return;

		if (Camera.Equals(null))
		{
			GD.PrintErr("Camera not found.");
			return;
		}

		if (!_targetObj.Equals(null))
		{
			// move towards player
			Position = Position.Lerp(_targetObj.Position+new Vector3(0, 0, 0), 0.1f);
		}

		_mouseVTracker.Update(delta);
		

		// Check for input and start rotation
		if (!_rotating && (Input.IsActionJustPressed("rotate_camera_sunwise") || Input.IsActionJustPressed("rotate_camera_widdershins")))
		{
			StartRotation(Input.IsActionPressed("rotate_camera_sunwise"));
		}

		// Handle rotation if in progress
		if (_rotating) PerformRotation();

		if (Input.IsActionPressed("zoom_camera"))
			Camera.Size = Mathf.Lerp(Camera.Size, CameraZoomMax, 0.5f);
		else Camera.Size = Mathf.Lerp(Camera.Size, CameraZoomMin, 0.5f);
	}

	private void StartRotation(bool clockwise)
	{
		_rotating = true; // Indicate that rotation is in progress
		_currSubdivision = clockwise ? _currSubdivision+1 : _currSubdivision + (RotationSubdivisions - 1);
		_currSubdivision %= RotationSubdivisions;

		_targetRotation = Rotation.Y + Mathf.DegToRad(_rotationAngle) * (clockwise ? 1: -1); // Rotate 90 degrees
		_initialRotation = Rotation.Y; // Record the current rotation
		_rotationStartTime = Time.GetTicksMsec() / 1000f; // Get the current time in seconds
	}

	private void PerformRotation()
	{
		// Calculate elapsed time

		float elapsedTime = (Time.GetTicksMsec() / 1000f) - _rotationStartTime;
		float t = elapsedTime / RotationDuration;

		// Logistic curve parameters
		float k = 12f; // Adjust this to control steepness
		float logisticT = 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));

		// Check if rotation is complete
		if (t >= 1f)
		{
			Rotation = new Vector3(Rotation.X, _targetRotation, Rotation.Z); // Set final rotation
			_rotating = false; // End rotation
			if (_currSubdivision == 0)
			{
				// set base rotation to be the same as the first fixed _camera angle
				Rotation = new Vector3(Rotation.X, Mathf.DegToRad(RotationOffset), Rotation.Z);
			}
			return; // Exit function after setting final rotation
		}

		// Interpolate rotation using the logistic curve
		float newRotationY = Mathf.Lerp(_initialRotation, _targetRotation, logisticT);
		Rotation = new Vector3(Rotation.X, newRotationY, Rotation.Z); // Apply rotation
	}
}
