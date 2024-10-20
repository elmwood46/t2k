using Godot;
using System;

// [Tool]
public partial class CameraController : Node3D
{
	[Export]
	public float rotationDuration = 0.5f; // Duration to complete the rotation
	[Export]
	public int rotationSubdivisions = 4; // number of fixed _camera angles allowed
	[Export]
	public float rotationOffset = 30f; // starting rotation offset in degrees
	[Export]
	public float CameraZoomMin { get; set; } = 10f;
	[Export]
	public float CameraZoomMax { get; set; } = 20f;
	[Export]
	public float cameraZoomSpeed = 1f;
	private float _currentZoom = 20f;
	private Prop targetObj;
	private Vector3 cameraTarget;
	private MouseVelocityTracker _mouseVTracker;
	public Camera3D Camera => _camera;
	private Camera3D _camera;
	private bool tacticalCamera = false;
	private int currSubdivision = 0;  // track the current fixed _camera angle
	private bool rotating = false; // flag for animating _camera rotation
	private float targetRotation = 0f; // Target rotation in radians
	private float initialRotation; // Starting rotation in radians
	private float rotationStartTime; // Time when rotation started
	private float rotationAngle; // the angle which the _camera moves every time you press "R"

	public override void _Ready()
	{
		this.targetObj = GetNodeOrNull<Prop>("../PlayerCharacter");

		this._camera = GetNodeOrNull<Camera3D>("Camera");
		if (this._camera.Equals(null))
		{
			GD.PrintErr("Camera not found.");
			return;
		}

		_mouseVTracker = new MouseVelocityTracker(GetViewport());
		Rotation = new Vector3(Rotation.X, Mathf.DegToRad(rotationOffset), Rotation.Z);
		rotationAngle = 360f / (float)rotationSubdivisions;
		UpdateCameraZoom();
	}

	// process - rotate and zoom the _camera 
	public override void _Process(double delta)
	{

		this.targetObj = GetNodeOrNull<Prop>("../PlayerCharacter");
		if (!this.targetObj.Equals(null))
		{
			cameraTarget = this.targetObj.Position+new Vector3(0, 0, 0);
		}

		this.Position = Position.Lerp(cameraTarget, 0.1f);
		

		if (this._camera.Equals(null))
		{
			GD.PrintErr("Camera not found.");
			return;
		}

		if (Input.IsActionJustPressed("toggleCameraMode") && !rotating)
		{

		}

		_mouseVTracker.Update(delta);
		
		UpdateCameraZoom();

		// Check for input and start rotation
		if (Input.IsActionJustPressed("rotate_camera") && !rotating)
		{
			StartRotation();
		}

		// Handle rotation if in progress
		if (rotating)
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
			//GD.Print("set zoom to: ", _currentZoom);
		}
		else if (Input.IsActionJustPressed("ui_scroll_up"))
		{
			_currentZoom -= cameraZoomSpeed;
			//GD.Print("set zoom to: ", _currentZoom);
		}

		// Handle mouse movement while the middle button is pressed
		if (Input.IsActionPressed("mb_middle"))
		{
			_currentZoom += _mouseVTracker.GetMouseVelocity().X / 2000;
			//GD.Print("set zoom to: ", _currentZoom);
		}

		// Clamp the zoom level to prevent it from going out of bounds
		_currentZoom = Mathf.Clamp(_currentZoom, CameraZoomMin, CameraZoomMax);

		// Set the camera size to the current zoom level
		this._camera.Size = _currentZoom;
	}


	private void ToggleCameraMode()
	{

		rotating = true;
	}

	private void StartRotation()
	{
		currSubdivision += 1;
		// Set the target rotation
		targetRotation = Rotation.Y + Mathf.DegToRad(rotationAngle); // Rotate 90 degrees
		initialRotation = Rotation.Y; // Record the current rotation
		rotationStartTime = Time.GetTicksMsec() / 1000f; // Get the current time in seconds
		rotating = true; // Indicate that rotation is in progress
	}

	private void PerformRotation(float delta)
	{
		// Calculate elapsed time

		float elapsedTime = (Time.GetTicksMsec() / 1000f) - rotationStartTime;
		float t = elapsedTime / rotationDuration;

		// Logistic curve parameters
		float k = 12f; // Adjust this to control steepness
		float logisticT = 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));

		// Check if rotation is complete
		if (t >= 1f)
		{
			Rotation = new Vector3(Rotation.X, targetRotation, Rotation.Z); // Set final rotation
			rotating = false; // End rotation
			if (currSubdivision == rotationSubdivisions)
			{
				currSubdivision = 0;
				Rotation = new Vector3(Rotation.X, Mathf.DegToRad(rotationOffset), Rotation.Z);
			}
			return; // Exit function after setting final rotation
		}

		// Interpolate rotation using the logistic curve
		float newRotationY = Mathf.Lerp(initialRotation, targetRotation, logisticT);
		Rotation = new Vector3(Rotation.X, newRotationY, Rotation.Z); // Apply rotation
	}
}