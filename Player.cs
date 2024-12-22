using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
	[Export] public Node3D Head { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public RayCast3D RayCast { get; set; }
	[Export] public MeshInstance3D BlockHighlight { get; set; }
    [Export] public RayCast3D RayDetectWall { get; set; }

	[Export] private float _mouseSensitivity = 0.3f;
    [Export] public float WALK_SPEED = 5.0f;
    [Export] public float SPRINT_SPEED = 8.0f;
    [Export] public float JUMP_VELOCITY = 4.8f;
    [Export] public float SENSITIVITY = 0.004f;
    private float _movespeed;

    //bob variables
    const float BOB_FREQ = 2.4f;
    const float BOB_AMP = 0.08f;
    private float t_bob = 0.0f;

    //fov variables
    const float BASE_FOV = 75.0f;
    const float FOV_CHANGE = 1.5f;

	[Export] private float _jumpVelocity = 10f;

	private float _cameraXRotation;

	private Timer spawnTimer;

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
		spawnTimer = new Timer();
		AddChild(spawnTimer);
		spawnTimer.WaitTime = 0.1f;
		spawnTimer.OneShot = true;
		spawnTimer.Start();
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

	public override void _Process(double delta)
	{
		if (!spawnTimer.IsStopped()) return;

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
		if (!spawnTimer.IsStopped()) return;

		var velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * (float)delta;
		}

		if (Input.IsActionJustPressed("Jump") && IsOnFloor())
		{
			velocity.Y = _jumpVelocity;
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
        if (IsOnFloor()) {
            if (direction.Length() > 0.1) {
                velocity.X = direction.X * _movespeed;
                velocity.Z = direction.Z * _movespeed;
            } else {
                velocity.X = Mathf.Lerp(velocity.X, direction.X * speed, (float)delta * 7.0f);
                velocity.z = Mathf.Lerp(velocity.Z, direction.Z * speed, (float)delta * 7.0f);
            }
        } else {
            velocity.X = Mathf.Lerp(velocity.X, direction.X * speed, (float)delta * 3.0f);
            velocity.Z = Mathf.Lerp(velocity.Z, direction.Z * speed, (float)delta * 3.0f);
        }

        // Wall detection and moving up ---------------------------------------------------------------
        // Check if a wall was hit
        if (IsOnFloor() && RayDetectWall.IsColliding() && RayDetectWall.GetCollider() is Chunk chunk)
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
                if (chunk.GetBlock(intBlockPosition + new Vector3I(0,i,0)) != BlockManager.Instance.Air) {

                    canJump = false;
                    break;
                }
                i++;
            }

            if (canJump) { // free space above
                Vector3 xz = velocity.Length() < 0.01 ? direction : velocity;
                xz.Y = 0;
                xz = xz.Normalized();
                if (xz.Dot(new Vector3(0,0,-1) * Head.GlobalBasis.Z) > 0.0f) {
                    GlobalPosition += new Vector3(xz.X,1,xz.Z);
                }
            }
        }
        // ---------------------------------------------------------------

		Velocity = velocity;

        //Head bob
        t_bob += (float)delta * velocity.length() * (float)IsOnFloor();
        Camera.Transform.Origin = Headbob(t_bob);

        // FOV
        var velocity_clamped = Mathf.Clamp(velocity.length(), 0.5, SPRINT_SPEED * 2);
        var target_fov = BASE_FOV + FOV_CHANGE * velocity_clamped;
        Camera.fov = lerp(camera.fov, target_fov, delta * 8.0f);

		MoveAndSlide();
	}

    private Vector3 Headbob(float time) {
        var pos = Vector3.Zero;
        pos.y = Mathf.Sin(time * BOB_FREQ) * BOB_AMP;
        pos.x = Mathf.Cos(time * BOB_FREQ / 2) * BOB_AMP;
        return pos;
    }
}