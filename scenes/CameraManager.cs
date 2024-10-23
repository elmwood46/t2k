using Godot;
using System;

public partial class CameraManager : Node3D
{
	[Export] public float rotationDuration = 0.5f; // Duration to complete the rotation
	[Export] public int rotationSubdivisions = 4; // number of fixed _camera angles allowed
	[Export] public float rotationOffset = 30f; // starting rotation offset in degrees
	[Export] public float CameraZoomMin { get; set; } = 10f;
	[Export] public float CameraZoomMax { get; set; } = 20f;
	[Export] public float cameraZoomSpeed = 1f;
	[Export] public Camera3D Camera;
	public static CameraManager Instance { get; private set; }
	private MouseVelocityTracker _mouseVTracker;
	private Prop targetObj;
	private int _currSubdivision = 0;  // track the current fixed _camera angle
	private bool _rotating = false; // flag for animating _camera rotation
	private float _targetRotation = 0f; // Target rotation in radians
	private float _initialRotation; // Starting rotation in radians
	private float _rotationStartTime; // Time when rotation started
	private float _rotationAngle; // the angle which the _camera moves every time you press "R"
	private float _currentZoom = 20f;

	public override void _Ready()
	{
		Instance = this;
		targetObj = Player.Instance;
		_mouseVTracker = new MouseVelocityTracker(GetViewport());
		Rotation = new Vector3(Rotation.X, Mathf.DegToRad(rotationOffset), Rotation.Z);
		_rotationAngle = 360f / (float)rotationSubdivisions;
		UpdateCameraZoom();
	}

	// process - rotate and zoom the _camera 
	public override void _Process(double delta)
	{
		if (Camera.Equals(null))
		{
			GD.PrintErr("Camera not found.");
			return;
		}

		if (!targetObj.Equals(null))
		{
			// move towards player
			Position = Position.Lerp(targetObj.Position+new Vector3(0, 0, 0), 0.1f);
		}

		_mouseVTracker.Update(delta);

		UpdateCameraZoom();

		// Check for input and start rotation
		if (Input.IsActionJustPressed("rotate_camera") && !_rotating)
		{
			StartRotation();
		}

		// Handle rotation if in progress
		if (_rotating)
		{
			PerformRotation((float)delta);
		}
	}

	private void UpdateCameraZoom()
	{
		// Detect scroll input continuously
		if (Input.IsActionJustPressed("ui_scroll_down"))
		{
			_currentZoom += cameraZoomSpeed;
			GD.Print("set zoom to: ", _currentZoom);
		}
		else if (Input.IsActionJustPressed("ui_scroll_up"))
		{
			_currentZoom -= cameraZoomSpeed;
			GD.Print("set zoom to: ", _currentZoom);
		}

		// Handle mouse movement while the middle button is pressed
		if (Input.IsActionPressed("mb_middle"))
		{
			_currentZoom += _mouseVTracker.GetMouseVelocity().X / 2000;
			GD.Print("set zoom to: ", _currentZoom);
		}

		// Clamp the zoom level to prevent it from going out of bounds
		_currentZoom = Mathf.Clamp(_currentZoom, CameraZoomMin, CameraZoomMax);

		// Set the camera size to the current zoom level
		Camera.Size = _currentZoom;
	}


	private void ToggleCameraMode()
	{

		_rotating = true;
	}

	private void StartRotation()
	{
		_currSubdivision += 1;
		// Set the target rotation
		_targetRotation = Rotation.Y + Mathf.DegToRad(_rotationAngle); // Rotate 90 degrees
		_initialRotation = Rotation.Y; // Record the current rotation
		_rotationStartTime = Time.GetTicksMsec() / 1000f; // Get the current time in seconds
		_rotating = true; // Indicate that rotation is in progress
	}

	private void PerformRotation(float delta)
	{
		// Calculate elapsed time

		float elapsedTime = (Time.GetTicksMsec() / 1000f) - _rotationStartTime;
		float t = elapsedTime / rotationDuration;

		// Logistic curve parameters
		float k = 12f; // Adjust this to control steepness
		float logisticT = 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));

		// Check if rotation is complete
		if (t >= 1f)
		{
			Rotation = new Vector3(Rotation.X, _targetRotation, Rotation.Z); // Set final rotation
			_rotating = false; // End rotation
			if (_currSubdivision == rotationSubdivisions)
			{
				_currSubdivision = 0;
				Rotation = new Vector3(Rotation.X, Mathf.DegToRad(rotationOffset), Rotation.Z);
			}
			return; // Exit function after setting final rotation
		}

		// Interpolate rotation using the logistic curve
		float newRotationY = Mathf.Lerp(_initialRotation, _targetRotation, logisticT);
		Rotation = new Vector3(Rotation.X, newRotationY, Rotation.Z); // Apply rotation
	}
}
