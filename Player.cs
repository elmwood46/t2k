using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class Player : CharacterBody3D, ISaveStateLoadable, IHurtable
{
	[Export] public int MaxHealth = 100;
	public int CurrentHealth { get; private set; }

	[Export] public Node3D Head { get; set; }
	[Export] public Node3D HeadCrouched { get; set; }
	[Export] public CollisionShape3D CollisionShape { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public Node3D CameraSmooth {get; set;}
	[Export] public Node3D CameraShake {get; set;}
	[Export (PropertyHint.Range,"0.01,1.5,0.01")] public float MaxCameraShake = 0.5f;
	[Export (PropertyHint.Range,"0.001,0.05,0.001")] public double CameraShakeDecay { get; private set; } = 0.05f;
	private double _camera_shake_amount = 0.0f;
	[Export] public Area3D CoinPickupArea {get;set;}
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

	private RigidBody3D _held_object = null;
	const float MAX_PICKUP_MASS = 100.0f;
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
	private Vector3 _savedCameraGlobalPos;
	private Vector3 _cameraPosReset = new(float.PositiveInfinity,999,999);

	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	// animation
	private AnimationTree _animationTree;
	private AnimationNodeStateMachinePlayback _stateMachinePlayback;

	private int _money = 0;

	public static Player Instance { get; private set; }

    public override string ToString() 
    {
        return $"Global Position: {Instance.GlobalPosition}\nMoney: {Instance._money}";
    }

	#region ready
	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		Instance = this;
		_crouch_speed = WalkSpeed * 0.8f;
		_standing_height = ((CapsuleShape3D)CollisionShape.Shape).Height;

		BlockHighlight.Scale = new Vector3(ChunkManager.VOXEL_SCALE + 0.05f,ChunkManager.VOXEL_SCALE + 0.05f,ChunkManager.VOXEL_SCALE + 0.05f);

		_animationTree = GetNode<AnimationTree>("WorldModel/AnimationTree");
		_stateMachinePlayback = (AnimationNodeStateMachinePlayback)_animationTree.Get("parameters/playback");

		// set the world models invisible to camera so the character model does not clip through the camera
		UpdateViewAndWorldModelMasks();
		
        _movespeed = WalkSpeed;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
	#endregion
	#region input
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
	#endregion
	#region load saved state
	public void LoadSavedState()
	{
		GlobalPosition = SaveManager.GetCachedPlayerPosition();
		Head.Rotation = Vector3.Zero;
		Head.RotateY(SaveManager.GetCachedHeadYRotation());
	}
	#endregion

	#region process input
	public override void _Process(double delta)
	{
		#region interactable
		// interact with objects
		InteractableComponent interactable = GetInteractableComponentAtShapecast();
		if (interactable != null) {
			interactable.HoverCursor(this);
			if (Input.IsActionJustPressed("Interact")) {
				interactable.Interact();
			}
		}
		#endregion

		#region debug

		// HACK debug restart
		if (Input.IsActionJustReleased("DebugRestart"))
		{
			GetTree().ReloadCurrentScene();
		}
		//HACK toggle wireframe
		if (Input.IsActionJustReleased("ToggleWireframe")) {
			if (GetViewport().DebugDraw==Viewport.DebugDrawEnum.Wireframe) {
				RenderingServer.SetDebugGenerateWireframes(false);
				GetViewport().DebugDraw=Viewport.DebugDrawEnum.Disabled;
			} else {
				RenderingServer.SetDebugGenerateWireframes(true);
				GetViewport().DebugDraw=Viewport.DebugDrawEnum.Wireframe;
			}
		}
		#endregion


		#region saving and loading
		if (Input.IsActionJustReleased("Reload"))
		{
			SaveManager.LoadSavedState();
		}

		// SAVING GAME 
        // store position and rotation in save manager
		if (Input.IsActionJustReleased("SaveGame"))
		{
			SaveManager.SaveToFile();
		}
		#endregion


		#region block break
		// do cube fragmenting stuff
		if (RayCast.IsColliding() && RayCast.GetCollider() is Node3D pb) {
			if (Input.IsActionJustPressed("Break")) {
				//if (pb.GetParent().GetParent() is DestructibleMesh mesh)
					//mesh.Break(RayCast.GetCollisionPoint(),PushForce);
			}
		}

        // do block modify stuff
		if (RayCast.IsColliding() && RayCast.GetCollider() is Chunk chunk)
		{
			BlockHighlight.Visible = true;
			var collision_pos = RayCast.GetCollisionPoint() - 0.5f * ChunkManager.VOXEL_SCALE * RayCast.GetCollisionNormal();
			BlockHighlight.GlobalPosition = ChunkManager.VOXEL_SCALE * ((Vector3)ChunkManager.GlobalPositionToBlockPosition(collision_pos) + Vector3.One * 0.5f);

			if (Input.IsActionJustPressed("Break"))
			{
				ChunkManager.TryDamageBlock(collision_pos, 5000);

				bool damage_line, damage_sphere;
				damage_line = false;
				damage_sphere = false;
				if (damage_line) // damage line in looking direction
					 ChunkManager.DamageLine(collision_pos,-Camera.GlobalTransform.Basis.Z.Normalized(),40,1000,4,false);
				if (damage_sphere)
					ChunkManager.DamageSphere(collision_pos,5,100,true);
			}

			if (Input.IsActionJustPressed("Place"))
			{
				ChunkManager.TrySetBlock(collision_pos + RayCast.GetCollisionNormal()*ChunkManager.VOXEL_SCALE, BlockManager.BlockID("Stone"));
			}
		}
		else
		{
			BlockHighlight.Visible = false;
		}
		#endregion

		CheckForCoins();
		AlignWorldModelToLookDir();
		UpdateRecoil((float) delta);
		UpdateAnimations();
	}
	#endregion
	#region physics process
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
		// DEBUG we increase jump speed
		if (Input.IsActionJustPressed("Jump"))// && (IsOnFloor() || _snappedToStairsLastFrame))
		{
			velocity.Y = JumpVelocity;
		} else {
			if (velocity.Y > JumpVelocity) {
				velocity.Y = JumpVelocity;
			}
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
		// DEBUG we increase sprint speed
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
		DoCameraShake();

        // FOV
        var velocity_clamped = Mathf.Clamp(velocity.Length(), 0.5f, SprintSpeed * 2.0f);
        float target_fov = BASE_FOV + FOV_CHANGE * velocity_clamped;
        Camera.Fov = Mathf.Lerp(Camera.Fov, target_fov, 0.25f);
	}
	#endregion
}