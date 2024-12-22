using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
	[Export] public Node3D Head { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public Node3D CameraSmooth {get; set;}
	[Export] public RayCast3D RayCast { get; set; }
	[Export] public MeshInstance3D BlockHighlight { get; set; }
    [Export] public RayCast3D StairsAheadRay { get; set; }
	[Export] public RayCast3D StairsBelowRay { get; set; }

	[Export] private float _mouseSensitivity = 0.3f;
    [Export] public float WALK_SPEED = 5.0f;
    [Export] public float SPRINT_SPEED = 8.0f;
    [Export] public float JUMP_VELOCITY = 4.8f;
    [Export] public float SENSITIVITY = 0.004f;
    private float _movespeed;

	const float MAX_STEP_HEIGHT = 0.55f; // Raycasts length should match this. StairsAhead one should be slightly longer.
	bool _snappedToStairsLastFrame = false;
	int _lastFrameOnFloor = -1;
	private Vector3 _savedCameraGlobalPos ; 
	private Vector3 _cameraPosReset = new Vector3(999,999,999);

    //bob variables
    const float BOB_FREQ = 2.4f;
    const float BOB_AMP = 0.08f;
    private float t_bob = 0.0f;

    //fov variables
    const float BASE_FOV = 75.0f;
    const float FOV_CHANGE = 1.5f;

	private float _cameraXRotation;

	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	public static Player Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
        _movespeed = WALK_SPEED;
		if (SaveManager.Instance.SaveFileExists())
		{
			this.Position = SaveManager.Instance.LoadPlayerPosition();
			this.Head.RotateY(SaveManager.Instance.State.Data.HeadRotation);
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
			var deltaX = mouseMotion.Relative.Y * _mouseSensitivity;
			var deltaY = -mouseMotion.Relative.X * _mouseSensitivity;

			Head.RotateY(Mathf.DegToRad(deltaY));
			if (_cameraXRotation + deltaX > -90 && _cameraXRotation + deltaX < 90)
			{
				Camera.RotateX(Mathf.DegToRad(-deltaX));
				_cameraXRotation += deltaX;
			}
		}
	}

	private void SaveCameraPosForSmoothing() {
		if (_savedCameraGlobalPos==_cameraPosReset) {
			_savedCameraGlobalPos = CameraSmooth.GlobalPosition;
		}
	}

	private void ResetCameraSmooth(float delta) {
		if (_savedCameraGlobalPos==_cameraPosReset) return;
		CameraSmooth.GlobalPosition = new Vector3 (CameraSmooth.GlobalPosition.X,_savedCameraGlobalPos.Y, CameraSmooth.GlobalPosition.Z);
		CameraSmooth.Position = new Vector3(CameraSmooth.Position.X,Mathf.Clamp(CameraSmooth.Position.Y, -0.7f, 0.7f),CameraSmooth.Position.Z);
		var move_amount = Mathf.Max(Velocity.Length() * delta, WALK_SPEED/2 * delta);
		CameraSmooth.Position = new Vector3(CameraSmooth.Position.X,Mathf.Lerp(CameraSmooth.Position.Y, 0f, move_amount),CameraSmooth.Position.Z);
		if (CameraSmooth.Position.Y == 0f) {
			_savedCameraGlobalPos = _cameraPosReset;
		}
	}

	private bool SnapUpStairsCheck(float delta)
	{
		if (!(IsOnFloor() || _snappedToStairsLastFrame)) return false;
		if (Velocity.Y > 0 || (Velocity * new Vector3(1,0,1)).Length() == 0) return false;

		var expectedMoveMotion = Velocity * new Vector3(1, 0, 1) * delta;
		var stepPosWithClearance = GlobalTransform.Translated(expectedMoveMotion + new Vector3(0, MAX_STEP_HEIGHT * 2, 0));

		var downCheckResult = new KinematicCollision3D();
		if (TestMove(stepPosWithClearance, new Vector3(0, -MAX_STEP_HEIGHT * 2, 0), downCheckResult) && (downCheckResult.GetCollider() is StaticBody3D || downCheckResult.GetCollider() is Chunk))
		{
			var stepHeight = ((stepPosWithClearance.Origin + downCheckResult.GetTravel()) - GlobalPosition).Y;
			if (stepHeight > MAX_STEP_HEIGHT || stepHeight <= 0.01 || (downCheckResult.GetPosition() - GlobalPosition).Y> MAX_STEP_HEIGHT) return false;

			StairsAheadRay.GlobalPosition = downCheckResult.GetPosition() + new Vector3(0, MAX_STEP_HEIGHT, 0) + expectedMoveMotion.Normalized() * 0.1f;
			StairsAheadRay.ForceRaycastUpdate();

			if (StairsAheadRay.IsColliding() && !IsSurfaceTooSteep(StairsAheadRay.GetCollisionNormal()))
			{
				SaveCameraPosForSmoothing();
				GlobalPosition = stepPosWithClearance.Origin + downCheckResult.GetTravel();
				ApplyFloorSnap();
				_snappedToStairsLastFrame = true;
				return true;
			}
		}

		return false;
	}

	private void SnapDownToStairsCheck() {
		var didSnap = false;
		StairsBelowRay.ForceRaycastUpdate();
		var floorBelow = StairsBelowRay.IsColliding() && !IsSurfaceTooSteep(StairsBelowRay.GetCollisionNormal());
		var wasOnFloorLastFrame = ((int)Engine.GetPhysicsFrames() == _lastFrameOnFloor);
		if (!IsOnFloor() && Velocity.Y <= 0 && (wasOnFloorLastFrame || _snappedToStairsLastFrame) && floorBelow)
		{
			var bodyTestResult = new KinematicCollision3D();
			if (TestMove(GlobalTransform, new Vector3(0, -MAX_STEP_HEIGHT, 0), bodyTestResult))
			{
				SaveCameraPosForSmoothing();
				var translateY = bodyTestResult.GetTravel().Y;
				Position += new Vector3(0, translateY, 0);
				ApplyFloorSnap();
				didSnap = true;
			}
		}
		_snappedToStairsLastFrame = didSnap;
	}

	private bool IsSurfaceTooSteep(Vector3 normal)
	{
		return normal.AngleTo(Vector3.Up) > FloorMaxAngle;
	}

	//private bool RunBodyTestMotion

	public override void _Process(double delta)
	{

        // do block modify stuff
		if (RayCast.IsColliding() && RayCast.GetCollider() is Chunk chunk)
		{
			BlockHighlight.Visible = true;

			var blockPosition = RayCast.GetCollisionPoint() - 0.5f * RayCast.GetCollisionNormal();
			var intBlockPosition = new Vector3I(
				Mathf.FloorToInt(blockPosition.X),
				Mathf.FloorToInt(blockPosition.Y*2),
				Mathf.FloorToInt(blockPosition.Z));
			BlockHighlight.GlobalPosition = new Vector3(
					Mathf.FloorToInt(blockPosition.X),
					Mathf.FloorToInt(blockPosition.Y*2)/2.0f,
					Mathf.FloorToInt(blockPosition.Z)
				)
				+ new Vector3(0.5f, 0.25f, 0.5f);

			if (Input.IsActionJustPressed("Break"))
			{
				// can't break lava
				Block b = chunk.GetBlock((Vector3I)(intBlockPosition - chunk.GlobalPosition));

				if (b != BlockManager.Instance.Lava) {
					//ChunkManager.Instance.DamageBlocks(new Vector3I[] {(Vector3I)(intBlockPosition - chunk.GlobalPosition)}, 5);

						// LINE ATTACK PATTERN
						Dictionary<Vector3I,int> blockDamages = new();
						int l = 20;
						int w = 4;
						Basis rot = new(Vector3.Up, Head.Rotation.Y);
						for (int x = -w/2; x <= w/2; x++)
						{
							for (int y = -w/2; y <= w/2; y++)
							{
								for (int z = -l; z <= 0; z++)
								{
									Vector3I bpos = intBlockPosition + (Vector3I)(rot *new Vector3(x, y, z));
									int damage = Mathf.Max(20+z,0);
									blockDamages[bpos] = damage;
								}
							}
						}
						ChunkManager.Instance.DamageBlocks(blockDamages);

                    // SPHERICAL BLOCK DAMAGE PATTERN
					/*List<Vector3I> blocksInSphere = new();
					int r = Mathf.CeilToInt(10.0f);

					for (int x = -r; x <= r; x++)
					{
						for (int y = -r; y <= r; y++)
						{
							for (int z = -r; z <= r; z++)
							{
								Vector3I bpos = intBlockPosition + new Vector3I(x, y, z);

								// Check if the block is within the sphere
								if (bpos.Y >= 0 && bpos.Y <=Chunk.Dimensions.Y && intBlockPosition.DistanceTo(bpos) <= r)
								{
									blocksInSphere.Add(bpos);
								}
							}
						}
					}
					ChunkManager.Instance.DamageBlocks(blocksInSphere.ToArray(), Mathf.CeilToInt(5));*/
				}
			}

			if (Input.IsActionJustPressed("Place"))
			{
				ChunkManager.Instance.SetBlock((Vector3I)(intBlockPosition + RayCast.GetCollisionNormal()), BlockManager.Instance.Stone);
			}

			if (Input.IsActionJustPressed("debug_reload"))
			{
				GetTree().ReloadCurrentScene();
			}
		}
		else
		{
			BlockHighlight.Visible = false;
		}

        // SAVING GAME 
        // store position and rotation in save manager
		SaveManager.Instance.State.Data.PlayerPosition = (Position.X, Position.Y, Position.Z);
		SaveManager.Instance.State.Data.HeadRotation = this.Head.Rotation.Y;

		if (Input.IsActionJustPressed("SaveGame"))
		{
			SaveManager.Instance.SaveGame();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsOnFloor() || _snappedToStairsLastFrame)
		{
			_lastFrameOnFloor = (int)Engine.GetPhysicsFrames();
		}

		var velocity = Velocity;

		if (!(IsOnFloor() || _snappedToStairsLastFrame))
		{
			velocity.Y -= _gravity * (float)delta;
		}

		if (Input.IsActionJustPressed("Jump") && (IsOnFloor() || _snappedToStairsLastFrame))
		{
			velocity.Y = JUMP_VELOCITY;
		}

        // set direction
        // Forward is the negative Z direction
		var inputDirection = Input.GetVector("Left", "Right", "Back", "Forward").Normalized();
		var direction = Vector3.Zero;
		direction += inputDirection.X * Head.GlobalBasis.X;
		direction += inputDirection.Y * -Head.GlobalBasis.Z;

        // check for sprint
        if (Input.IsActionPressed("Sprint")) {
            _movespeed = SPRINT_SPEED;
        } else {
            _movespeed = WALK_SPEED;
        }

        // lerp velocity
        if (IsOnFloor() || _snappedToStairsLastFrame) {
            if (direction.Length() > 0.1) {
                velocity.X = direction.X * _movespeed;
                velocity.Z = direction.Z * _movespeed;
            } else {
                velocity.X = Mathf.Lerp(velocity.X, direction.X * _movespeed, (float)delta * 7.0f);
                velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * _movespeed, (float)delta * 7.0f);
            }
        } else {
            velocity.X = Mathf.Lerp(velocity.X, direction.X * _movespeed, (float)delta * 3.0f);
            velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * _movespeed, (float)delta * 3.0f);
        }

		Velocity = velocity;

        //Head bob
        t_bob += (float)delta * velocity.Length() * ((IsOnFloor() || _snappedToStairsLastFrame) ? 1.0f : 0.0f);
        Camera.Position = Headbob(t_bob);

        // FOV
        var velocity_clamped = Mathf.Clamp(velocity.Length(), 0.5f, SPRINT_SPEED * 2.0f);
        float target_fov = BASE_FOV + FOV_CHANGE * velocity_clamped;
        Camera.Fov = Mathf.Lerp(Camera.Fov, target_fov, 0.25f);


		if (!SnapUpStairsCheck((float)delta))
		{
			MoveAndSlide();
			SnapDownToStairsCheck();
		}	

		ResetCameraSmooth((float)delta);
	}

    private Vector3 Headbob(float time) {
        var pos = Vector3.Zero;
        pos.Y = Mathf.Sin(time * BOB_FREQ) * BOB_AMP;
        pos.X = Mathf.Cos(time * BOB_FREQ / 2) * BOB_AMP;
        return pos;
    }
}