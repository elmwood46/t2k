using Godot;

public partial class TestPlayer : CharacterBody3D
{
    [Export] public float Speed = 10f;
    [Export] public float Acceleration = 5f;
    [Export] public float Deceleration = 10f;
    [Export] public float JumpForce = 5f;
    [Export] public float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    [Export] public float WallDetectionDistance = 1.5f;  // Raycast distance to check for walls
    [Export] public float WallHeightThreshold = 0.5f;  // Wall height threshold for moving up
    [Export] public RayCast3D RayDetectWall { get; set; }

    [Export] public Camera3D Camera;  // Reference to the Camera3D
    [Export] public Node3D CameraGimbal;
    [Export] public float SmoothSpeed = 5f;
    [Export] private float _mouseSensitivity = 0.3f;

    private Vector3 _velocity = Vector3.Zero;

    private float _cameraXRotation;

    public static TestPlayer Instance { get; private set; }

    public override void _Ready()
	{
         if (Camera == null)
        {
            GD.PrintErr("Camera is not assigned!");
        }
        Camera.LookAt(GlobalPosition, Vector3.Up);
        
		Instance = this;
		if (SaveManager.Instance.SaveFileExists())
		{
			this.Position = SaveManager.Instance.LoadPlayerPosition();
		} else {
			this.Position = new Vector3(0, 10, 0);
		}

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion)
		{
			var mouseMotion = @event as InputEventMouseMotion;

			var deltaY = -mouseMotion.Relative.X * _mouseSensitivity;
			CameraGimbal.RotateY(Mathf.DegToRad(deltaY));

		}
	}

    public override void _PhysicsProcess(double delta)
    {
        // Handle player movement
        Vector3 inputDirection = GetInputDirection();
        Vector3 horizontalVelocity = _velocity;
        horizontalVelocity.Y = 0;

        // Acceleration and Deceleration
        if (inputDirection != Vector3.Zero)
        {
            horizontalVelocity = horizontalVelocity.Lerp(inputDirection * Speed, (float)delta * Acceleration);
        }
        else
        {
            horizontalVelocity = horizontalVelocity.Lerp(Vector3.Zero, (float)delta * Deceleration);
        }

        // Apply gravity
        _velocity.Y -= Gravity * (float)delta;

        // Jump
        if (IsOnFloor() && Input.IsActionJustPressed("Jump"))
        {
            _velocity.Y = JumpForce;
        }

        // Wall detection and moving up ---------------------------------------------------------------
        // Check if a wall was hit
        if (RayDetectWall.IsColliding() && RayDetectWall.GetCollider() is Chunk chunk)
        {
			var blockPosition = RayDetectWall.GetCollisionPoint() - 0.5f * RayDetectWall.GetCollisionNormal();
			var intBlockPosition = new Vector3I(
				Mathf.FloorToInt(blockPosition.X),
				Mathf.FloorToInt(blockPosition.Y*2),
				Mathf.FloorToInt(blockPosition.Z));
            intBlockPosition = (Vector3I)(intBlockPosition - chunk.GlobalPosition);


            int i=1;
            bool canJump = true;
            while (i < 4) {
                if (intBlockPosition.Y + i >= Chunk.Dimensions.Y) {

                    break;
                }
                if (chunk.GetBlockIDFromPosition(intBlockPosition + new Vector3I(0,i,0)) == 0) { // CHANGED FROM AIR CHECK

                    canJump = false;
                    break;
                }
                i++;
            }

            if (canJump) { // free space above
                Vector3 xz = _velocity.Length() < 0.05 ? inputDirection : _velocity;
                xz.Y = 0;
                xz = xz.Normalized();
                if (xz.Dot(new Vector3(0,0,-1) * CameraGimbal.GlobalBasis.Z) > 0.5f) {
                    _velocity.Y = 2.5f;
                }
            }
        }
        // ---------------------------------------------------------------

        // Combine horizontal and vertical velocity
        _velocity.X = horizontalVelocity.X;
        _velocity.Z = horizontalVelocity.Z;

        Velocity = _velocity;

        // Move the player
        MoveAndSlide();

        // Camera follow logic
        if (Camera != null)
        {
            FollowCamera(delta);
        }
    }

    private Vector3 GetInputDirection()
    {
        var inputDirection = Input.GetVector("Left", "Right", "Forward", "Back").Normalized();
        Vector3 direction = Vector3.Zero;
        direction += inputDirection.X * CameraGimbal.GlobalBasis.X; 
        direction += inputDirection.Y * CameraGimbal.GlobalBasis.Z;
        return direction;
    }

    private void FollowCamera(double delta)
    {
        // Smoothly interpolate the camera's position
        CameraGimbal.GlobalTransform = new Transform3D(
            CameraGimbal.GlobalTransform.Basis,
            CameraGimbal.GlobalTransform.Origin.Lerp(GlobalPosition, (float)delta * SmoothSpeed)
        );

        // Make the camera look at the player
        //Camera.LookAt(GlobalPosition, Vector3.Up);
    }
}
