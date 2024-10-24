using Godot;
using System;
using System.Reflection.Metadata;

[Tool]
public partial class Player : Prop
{
	[Export] public float Health = 100;
	[Export] public int Money = 10;
	[Export] public int Ward = 5;
	[Export] public float Speed = 5.0f;
	public static Player Instance { get; private set; }

	public const float JumpVelocity = 4.5f;
	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
	private float _maxHealth;
	private Timer _spawnTimer;

	public Player()
	{
		_maxHealth = Health;
	}

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		Instance = this;
		if (SaveManager.Instance.SaveFileExists())
		{
			this.Position = SaveManager.Instance.LoadPlayerPosition();
		} else {
			this.Position = new Vector3(0, 10, 0);
		}

		// Input.MouseMode = Input.MouseModeEnum.Captured;
		_spawnTimer = new Timer();
		AddChild(_spawnTimer);
		_spawnTimer.WaitTime = 0.1f;
		_spawnTimer.OneShot = true;
		_spawnTimer.Start();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint()) return;
		if (!_spawnTimer.IsStopped()) return;

		HandleMovement(delta);

		if (Health <=0) Die();

		// store position in save manager
		SaveManager.Instance.State.Data.PlayerPosition = (Position.X, Position.Y, Position.Z);

		if (Input.IsActionJustPressed("save"))
		{
			SaveManager.Instance.SaveGame();
		}
	}

	private void Die()
    {
        // Implement death logic here
		DebugManager.Log($"{Title}: I'm dead!");
        QueueFree();
    }

	private void HandleMovement(double delta)
    {
		var velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * (float)delta;
			if (Position.Y < -100f) Die(); // fall out of map
		}

		var ipt = Input.GetVector("move_left","move_right","move_up","move_down").Normalized();

		var direction = new Vector3(ipt.X, 0f, ipt.Y)
		.Rotated(Vector3.Up, CameraManager.Instance.Rotation.Y)
		.Normalized();

		velocity.X = direction.X * Speed;
		velocity.Z = direction.Z * Speed;

		Velocity = velocity;
		MoveAndSlide();
    }
}
