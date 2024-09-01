using Godot;

public partial class CameraController : Node3D
{
	[Export]
	public float rotationDuration = 0.5f; // Duration to complete the rotation
	[Export]
	public int rotationSubdivisions = 4; // number of fixed _camera angles allowed
	[Export]
	public float rotationOffset = 30f; // starting rotation offset in degrees
	[Export]
	public float cameraZoomMin = 10f;
	[Export]
	public float cameraZoomMax = 20f;
	[Export]
	public float cameraZoomSpeed = 1.0f;
	private float currentZoom;
	private MouseVelocityTracker _mouseVTracker;

	public Camera3D Camera { get => _camera; private set => _camera = value; }
	private Camera3D _camera;
	private int currSubdivision = 0;  // track the current fixed _camera angle
	private bool rotating = false; // flag for animating _camera rotation
	private float targetRotation = 0f; // Target rotation in radians
	private float initialRotation; // Starting rotation in radians
	private float rotationStartTime; // Time when rotation started
	private float rotationAngle; // the angle which the _camera moves every time you press "R"

	public override void _Ready()
	{
		_camera = GetNodeOrNull<Camera3D>("Camera");
		if (_camera.Equals(null)) {
			GD.Print("Camera not found");
			return;
		}

		_mouseVTracker = new MouseVelocityTracker(GetViewport());
		this.Rotation = new Vector3(Rotation.X, Mathf.DegToRad(rotationOffset), Rotation.Z);
		rotationAngle = 360f / (float)rotationSubdivisions;
		currentZoom = cameraZoomMin;
		
		_camera.Size = currentZoom;
	}

	// process - rotate and zoom the _camera 
	public override void _Process(double delta)
	{

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
		currentZoom += ((Input.IsActionJustPressed("ui_scroll_down") ? 1 : 0) - (Input.IsActionJustPressed("ui_scroll_up") ? 1 : 0)) * cameraZoomSpeed;
		if (Input.IsActionPressed("mb_middle"))
		{
			currentZoom += _mouseVTracker.GetMouseVelocity().X / 2000;
		}
		currentZoom = Mathf.Clamp(currentZoom, cameraZoomMin, cameraZoomMax);
		this._camera.Size = currentZoom;
	}

	private void StartRotation()
	{
		currSubdivision += 1;
		// Set the target rotation
		targetRotation = this.Rotation.Y + Mathf.DegToRad(rotationAngle); // Rotate 90 degrees
		initialRotation = this.Rotation.Y; // Record the current rotation
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
			this.Rotation = new Vector3(Rotation.X, targetRotation, Rotation.Z); // Set final rotation
			rotating = false; // End rotation
			if (currSubdivision == rotationSubdivisions)
			{
				currSubdivision = 0;
				this.Rotation = new Vector3(Rotation.X, Mathf.DegToRad(rotationOffset), Rotation.Z);
			}
			return; // Exit function after setting final rotation
		}

		// Interpolate rotation using the logistic curve
		float newRotationY = Mathf.Lerp(initialRotation, targetRotation, logisticT);
		this.Rotation = new Vector3(Rotation.X, newRotationY, Rotation.Z); // Apply rotation
	}
}
