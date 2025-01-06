using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class Player : CharacterBody3D
{
	[Export] public Node3D Head { get; set; }
	[Export] public Node3D HeadCrouched { get; set; }
	[Export] public CollisionShape3D CollisionShape { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public Node3D CameraSmooth {get; set;}

	[Export] public RayCast3D RayCast { get; set; }
	[Export] public MeshInstance3D BlockHighlight { get; set; }
	[Export] public ShapeCast3D ShapeCast { get; set; }
    [Export] public RayCast3D StairsAheadRay { get; set; }
	[Export] public RayCast3D StairsBelowRay { get; set; }


	[Export] public float MouseSensitivity = 0.3f;
    [Export] public float WalkSpeed = 5.0f;
    [Export] public float SprintSpeed = 8.0f;
	[Export] public float ClimbSpeed = 7.0f;
    [Export] public float JumpVelocity = 4.8f;
	
	[Export] public Vector2 TargetRecoil = Vector2.Zero;
	[Export] public Vector2 CurrentRecoil = Vector2.Zero;
	const float RECOIL_APPLY_SPEED = 10f;
	const float  RECOIL_RECOVER_SPEED = 7f;
	[Export] public float Mass = 80.0f;
	[Export] public float PushForce = 5.0f;

    private float _movespeed; // used for tracking players current move speed after applying sprinting or crouching etc
	const float MAX_STEP_HEIGHT = 0.55f; // Raycasts length should match this. StairsAhead one should be slightly longer.
	private bool _snappedToStairsLastFrame = false;
	private ulong _lastFrameOnFloor = 99999999999UL;

	// ladder variables
	private Area3D _curLadderClimbing = null; 
	Vector3 _wish_dir = Vector3.Zero;
	Vector3 _cam_aligned_wish_dir = Vector3.Zero;
	
	const float CROUCH_TRANSLATE = 0.7F;
	const float CROUCH_JUMP_BOOST = CROUCH_TRANSLATE * 0.9f; // 0.9 makes the camera jitter when you crouch, it's tactile feedback 
	private bool _is_crouched = false;
	private float _crouch_speed; // crouch speed is 0.8* walk speed, set in ready method
	private float _standing_height; // set in ready method

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

	// animation
	private AnimationTree _animationTree;
	private AnimationNodeStateMachinePlayback _stateMachinePlayback;

	public static Player Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		_crouch_speed = WalkSpeed * 0.8f;
		_standing_height = ((CapsuleShape3D)CollisionShape.Shape).Height;

		BlockHighlight.Scale = new Vector3(Chunk.VOXEL_SCALE + 0.05f,Chunk.VOXEL_SCALE + 0.05f,Chunk.VOXEL_SCALE + 0.05f);

		_animationTree = GetNode<AnimationTree>("WorldModel/AnimationTree");
		_stateMachinePlayback = (AnimationNodeStateMachinePlayback)_animationTree.Get("parameters/playback");

		// set the world models invisible to camera so the character model does not clip through the camera
		UpdateViewAndWorldModelMasks();
		
        _movespeed = WalkSpeed;
		if (SaveManager.Instance.SaveFileExists())
		{
			//Position = SaveManager.Instance.LoadPlayerPosition();
			Head.RotateY(SaveManager.Instance.State.Data.HeadRotation);
		} else {
			//Position = new Vector3(0, 10, 0);
		}

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion)
		{
			var mouseMotion = @event as InputEventMouseMotion;
			var deltaX = mouseMotion.Relative.Y * MouseSensitivity;
			var deltaY = -mouseMotion.Relative.X * MouseSensitivity;

			Head.RotateY(Mathf.DegToRad(deltaY));
			if (_cameraXRotation + deltaX > -90 && _cameraXRotation + deltaX < 90)
			{
				Camera.RotateX(Mathf.DegToRad(-deltaX));
				_cameraXRotation += deltaX;
			}
		}
	}

	public override void _Process(double delta)
	{
		InteractableComponent interactable = GetInteractableComponentAtShapecast();
		if (interactable != null) {
			interactable.HoverCursor(this);
			if (Input.IsActionJustPressed("Interact")) {
				interactable.Interact();
			}
		}

        // do block modify stuff
		if (RayCast.IsColliding() && RayCast.GetCollider() is Chunk chunk)
		{
			BlockHighlight.Visible = true;
			var inv_vox_size = 1/Chunk.VOXEL_SCALE;

			var blockPosition = RayCast.GetCollisionPoint() - 0.5f * Chunk.VOXEL_SCALE * RayCast.GetCollisionNormal();
			blockPosition *= inv_vox_size;
			var intBlockPosition = new Vector3I(
				Mathf.FloorToInt(blockPosition.X),
				Mathf.FloorToInt(blockPosition.Y),
				Mathf.FloorToInt(blockPosition.Z));
			BlockHighlight.GlobalPosition = new Vector3(
					Mathf.FloorToInt(blockPosition.X)*Chunk.VOXEL_SCALE,
					Mathf.FloorToInt(blockPosition.Y)*Chunk.VOXEL_SCALE,
					Mathf.FloorToInt(blockPosition.Z)*Chunk.VOXEL_SCALE
				)
				+ new Vector3(0.5f, 0.5f, 0.5f)*Chunk.VOXEL_SCALE;
			//intBlockPosition.X = Math.Clamp(intBlockPosition.X, 0, Chunk.Dimensions.X);
			//intBlockPosition.Y = Math.Clamp(intBlockPosition.Y, 0, Chunk.Dimensions.Y);
			//intBlockPosition.Z = Math.Clamp(intBlockPosition.Z, 0, Chunk.Dimensions.Z);

			if (Input.IsActionJustPressed("Break"))
			{
				int b = chunk.GetBlockInfoFromPosition((Vector3I)(intBlockPosition - chunk.GlobalPosition*inv_vox_size));
				if (!Chunk.IsBlockInvincible(b)) {
					Dictionary<Vector3I,int> d = new()
					{
						[intBlockPosition] = 5000
					};
					ChunkManager.Instance.DamageBlocks(d);
				}

				if (true) {
					//ChunkManager.Instance.DamageBlocks(new Vector3I[] {(Vector3I)(intBlockPosition - chunk.GlobalPosition)}, 5);

						// LINE ATTACK PATTERN
						Dictionary<Vector3I,int> blockDamages = new();
						int l = 3;
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
				ChunkManager.Instance.SetBlock((Vector3I)(intBlockPosition + RayCast.GetCollisionNormal()), BlockManager.BlockID("Stone"));
			}

			if (Input.IsActionJustPressed("debug_reload"))
			{
				GetTree().ReloadCurrentScene();
			}

			if (Input.IsActionJustReleased("ToggleWireframe")) {
				if (GetViewport().DebugDraw==Viewport.DebugDrawEnum.Wireframe) {
					RenderingServer.SetDebugGenerateWireframes(false);
					GetViewport().DebugDraw=Viewport.DebugDrawEnum.Disabled;
				} else {
					RenderingServer.SetDebugGenerateWireframes(true);
					GetViewport().DebugDraw=Viewport.DebugDrawEnum.Wireframe;
				}
			}
		}
		else
		{
			BlockHighlight.Visible = false;
		}

		AlignWorldModelToLookDir();
		UpdateRecoil((float) delta);
		UpdateAnimations();

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
			_lastFrameOnFloor = Engine.GetPhysicsFrames();
		}

		// get velocity
		var velocity = Velocity;

		// apply gravity
		if (!(IsOnFloor() || _snappedToStairsLastFrame))
		{
			velocity.Y -= _gravity * (float)delta;
		}

		// jump
		if (Input.IsActionJustPressed("Jump"))// && (IsOnFloor() || _snappedToStairsLastFrame))
		{
			velocity.Y = JumpVelocity;
		}

        // set direction
        // Forward is the negative Z direction
		var inputDirection = Input.GetVector("Left", "Right", "Back", "Forward").Normalized();
		var direction = Vector3.Zero;
		direction += inputDirection.X * Head.GlobalBasis.X;
		direction += inputDirection.Y * -Head.GlobalBasis.Z;

		// get the direction you wish to move in global space
		// Depending on which way you have you character facing, you may have to negate the input directions
		_wish_dir = GlobalTransform.Basis * new Vector3(inputDirection.X, 0f, -inputDirection.Y);
		_cam_aligned_wish_dir = Camera.GlobalTransform.Basis * new Vector3(inputDirection.X, 0f, -inputDirection.Y);

		HandleCrouch((float)delta);

        // check for sprint speed adjustment
        if (Input.IsActionPressed("Sprint") && !_is_crouched) {
            _movespeed = SprintSpeed;
        } else {
            _movespeed = _is_crouched ? _crouch_speed : WalkSpeed;
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
				PushAwayRigidBodies();
				MoveAndSlide();
				SnapDownToStairsCheck();
			}	
		}

		ResetCameraSmooth((float)delta);

        // FOV
        var velocity_clamped = Mathf.Clamp(velocity.Length(), 0.5f, SprintSpeed * 2.0f);
        float target_fov = BASE_FOV + FOV_CHANGE * velocity_clamped;
        Camera.Fov = Mathf.Lerp(Camera.Fov, target_fov, 0.25f);
	}

}