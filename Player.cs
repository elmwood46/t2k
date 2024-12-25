using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
	[Export] public float CLIMB_SPEED = 7.0f;
    [Export] public float JUMP_VELOCITY = 4.8f;
    [Export] public float SENSITIVITY = 0.004f;

    private float _movespeed;
	const float MAX_STEP_HEIGHT = 0.50f; // Raycasts length should match this. StairsAhead one should be slightly longer.
	private bool _snappedToStairsLastFrame = false;
	private int _lastFrameOnFloor = -1;

	// ladder variables
	private Area3D _curLadderClimbing = null; 
	Vector3 _wish_dir = Vector3.Zero;
	Vector3 _cam_aligned_wish_dir = Vector3.Zero;
	
    //bob variables
    const float BOB_FREQ = 2.4f;
    const float BOB_AMP = 0.08f;
    private float t_bob = 0.0f;
    //fov variables
    const float BASE_FOV = 75.0f;
    const float FOV_CHANGE = 1.5f;

	//camera vals
	const int VIEW_MODEL_LAYER = 2;
	const int WORLD_MODEL_LAYER = 3;
	private float _cameraXRotation;
	private Vector3 _savedCameraGlobalPos ; 
	private Vector3 _cameraPosReset = new(float.PositiveInfinity,999,999);

	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	public static Player Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;

		// set the world models invisible to camera so the character model does not clip through the camera
		UpdateViewAndWorldModelMasks();
		
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

	public void UpdateViewAndWorldModelMasks() {
		SetCullLayerRecursive(GetNode<Node3D>("%HandWorldModel"), WORLD_MODEL_LAYER, false);
		SetCullLayerRecursive(GetNode<Node3D>("%HandViewModel"), VIEW_MODEL_LAYER, true);
		Camera.SetCullMaskValue(WORLD_MODEL_LAYER, false); // hide the world model layer
		//Camera.SetCullMaskValue(VIEW_MODEL_LAYER, false); // hide the view model layer -- e.g. for mirrors and other cameras
	}

    // Recursive method to process all child nodes
    private static void SetCullLayerRecursive(Node node, int cull_layer, bool disableShadows)
    {
        // Iterate over the current node's children
        foreach (Node child in node.GetChildren())
        {
            // Example: If it's a VisualInstance3D, modify properties or do other logic
            if (child is VisualInstance3D visual)
            {
                // Set Layer Mask (example action)
                visual.SetLayerMaskValue(1, false);  // Disable layer 1 (example)
                visual.SetLayerMaskValue(cull_layer, true);   // Enable layer 2 (example)
            }

			if (disableShadows && child is GeometryInstance3D g) {
				g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			}

            // If the child has its own children, recursively process them
            SetCullLayerRecursive(child, cull_layer, disableShadows);
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

	private bool HandleLadderPhysics(float delta) { // move around on ladder and return true if on ladder
		// Keep track of whether already on ladder
        bool wasClimbingLadder = (_curLadderClimbing != null) && _curLadderClimbing.OverlapsBody(this);
        if (!wasClimbingLadder)
        {
            _curLadderClimbing = null;
            foreach (Node node in GetTree().GetNodesInGroup("ladder_area3d"))
            {
                if (node is Area3D ladder && ladder.OverlapsBody(this))
                {
                    _curLadderClimbing = ladder;
                    break;
                }
            }
        }

        if (_curLadderClimbing == null)
            return false;

        // Set up variables
        Transform3D ladderTransform = _curLadderClimbing.GlobalTransform;
        Vector3 posRelToLadder = ladderTransform.AffineInverse() * GlobalPosition;

        float forwardMove = Input.GetActionStrength("Forward") - Input.GetActionStrength("Back");
        float sideMove = Input.GetActionStrength("Right") - Input.GetActionStrength("Left");

        Vector3 ladderForwardMove = ladderTransform.AffineInverse().Basis * 
                                    GetViewport().GetCamera3D().GlobalTransform.Basis * 
                                    new Vector3(0, 0, -forwardMove);

        Vector3 ladderSideMove = ladderTransform.AffineInverse().Basis * 
                                 GetViewport().GetCamera3D().GlobalTransform.Basis * 
                                 new Vector3(sideMove, 0, 0);

        // Strafe velocity
        float ladderStrafeVel = CLIMB_SPEED * (ladderSideMove.X + ladderForwardMove.X);

        // Climb velocity
        float ladderClimbVel = CLIMB_SPEED * -ladderSideMove.Z;
        float upWish = new Vector3(0, 1, 0).Rotated(new Vector3(1, 0, 0), Mathf.DegToRad(-45))
                                          .Dot(ladderForwardMove);
        ladderClimbVel += CLIMB_SPEED * upWish;

        // Dismount logic
        bool shouldDismount = false;

        if (!wasClimbingLadder)
        {
            bool mountingFromTop = posRelToLadder.Y > _curLadderClimbing.GetNode<Node3D>("TopOfLadder").Position.Y;
            if (mountingFromTop)
            {
                if (ladderClimbVel > 0) {
					GD.Print("dismounting from ladderClimbVel > 0 (mounting from top)");
					shouldDismount = true;
				}
            }
            else
            {
                if ((ladderTransform.AffineInverse().Basis * _wish_dir).Z >= 0) {
					GD.Print("dismounting from ladderTransform.AffineInverse().Basis * _wish_dir).Z >= 0");
                    shouldDismount = true;
				}
            }

            if (posRelToLadder.Z > 0.1f) {
				GD.Print("dismounting from Mathf.Abs(posRelToLadder.Z) > 0.1f");
				shouldDismount = true;
			}
        }

        if (IsOnFloor() && ladderClimbVel <= 0) {
			GD.Print("dismounting from floor and no climb vel");
			shouldDismount = true;
		}

		GD.Print(ladderClimbVel);
		GD.Print("currLadder ", _curLadderClimbing);
		GD.Print("shoulddismount ", shouldDismount);

        if (shouldDismount)
        {
            _curLadderClimbing = null;
            return false;
        }

        // Jump off ladder mid-climb
        if (wasClimbingLadder && Input.IsActionJustPressed("Jump"))
        {
            Velocity = _curLadderClimbing.GlobalTransform.Basis.Z * JUMP_VELOCITY * 1.5f;
            _curLadderClimbing = null;
            return false;
        }

        Velocity = ladderTransform.Basis * new Vector3(ladderStrafeVel, ladderClimbVel, 0);

        // Snap player onto ladder
        posRelToLadder.Z = 0;
        GlobalPosition = ladderTransform * posRelToLadder;

        MoveAndSlide();
        return true;
	}

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
			//intBlockPosition.X = Math.Clamp(intBlockPosition.X, 0, Chunk.Dimensions.X);
			//intBlockPosition.Y = Math.Clamp(intBlockPosition.Y, 0, Chunk.Dimensions.Y);
			//intBlockPosition.Z = Math.Clamp(intBlockPosition.Z, 0, Chunk.Dimensions.Z);

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
									Vector3I bpos = intBlockPosition + (Vector3I)(rot * new Vector3(x, y, z));
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

		// get velocity
		var velocity = Velocity;

		// apply gravity
		if (!(IsOnFloor() || _snappedToStairsLastFrame))
		{
			velocity.Y -= _gravity * (float)delta;
		}

		// jump
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

		// get the direction you wish to move in global space
		// Depending on which way you have you character facing, you may have to negate the input directions
		_wish_dir = this.GlobalTransform.Basis * new Vector3(inputDirection.X, 0f, -inputDirection.Y);
		_cam_aligned_wish_dir = Camera.GlobalTransform.Basis * new Vector3(inputDirection.X, 0f, -inputDirection.Y);

        // check for sprint speed adjustment
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

		if (!HandleLadderPhysics((float)delta)) { // handleladderphysics moves the player if they are on a ladder
			//Head bob
			t_bob += (float)delta * velocity.Length() * ((IsOnFloor() || _snappedToStairsLastFrame) ? 1.0f : 0.0f);
			Camera.Position = Headbob(t_bob);

			if (!SnapUpStairsCheck((float)delta))
			{
				MoveAndSlide();
				SnapDownToStairsCheck();
			}	
		}

		ResetCameraSmooth((float)delta);

        // FOV
        var velocity_clamped = Mathf.Clamp(velocity.Length(), 0.5f, SPRINT_SPEED * 2.0f);
        float target_fov = BASE_FOV + FOV_CHANGE * velocity_clamped;
        Camera.Fov = Mathf.Lerp(Camera.Fov, target_fov, 0.25f);
	}

    private static Vector3 Headbob(float time) {
        var pos = Vector3.Zero;
        pos.Y = Mathf.Sin(time * BOB_FREQ) * BOB_AMP;
        pos.X = Mathf.Cos(time * BOB_FREQ / 2) * BOB_AMP;
        return pos;
    }
}