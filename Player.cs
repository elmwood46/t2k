using Godot;
using System;

public partial class Player : CharacterBody3D
{
	[Export] public Node3D Head { get; set; }
	[Export] public Camera3D Camera { get; set; }
	[Export] public RayCast3D RayCast { get; set; }
	[Export] public MeshInstance3D BlockHighlight { get; set; }

	[Export] private float _mouseSensitivity = 0.3f;
	[Export] private float _movementSpeed = 16f;
	[Export] private float _jumpVelocity = 10f;

	private float _cameraXRotation;

	private Timer spawnTimer;

	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	public static Player Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
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

		if (RayCast.IsColliding() && RayCast.GetCollider() is Chunk chunk)
		{
			BlockHighlight.Visible = true;

			var blockPosition = (RayCast.GetCollisionPoint() - 0.5f * RayCast.GetCollisionNormal());
			var intBlockPosition = new Vector3I(Mathf.FloorToInt(blockPosition.X), Mathf.FloorToInt(blockPosition.Y*2), Mathf.FloorToInt(blockPosition.Z));
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
					chunk.DamageBlock((Vector3I)(intBlockPosition - chunk.GlobalPosition), 5);
					chunk.ForceUpdate();
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

		var inputDirection = Input.GetVector("Left", "Right", "Back", "Forward").Normalized();

		var direction = Vector3.Zero;

		direction += inputDirection.X * Head.GlobalBasis.X;

		// Forward is the negative Z direction
		direction += inputDirection.Y * -Head.GlobalBasis.Z;

		velocity.X = direction.X * _movementSpeed;
		velocity.Z = direction.Z * _movementSpeed;

		Velocity = velocity;
		MoveAndSlide();

		// store position and rotation in save manager
		SaveManager.Instance.State.Data.PlayerPosition = (Position.X, Position.Y, Position.Z);
		SaveManager.Instance.State.Data.HeadRotation = this.Head.Rotation.Y;

		if (Input.IsActionJustPressed("SaveGame"))
		{
			SaveManager.Instance.SaveGame();
		}
	}
}