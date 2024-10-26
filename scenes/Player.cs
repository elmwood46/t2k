using Godot;

[Tool]
public partial class Player : Prop
{
	[Export] public float Health = 100;
	[Export] public int Money = 10;
	[Export] public int Ward = 5;
	[Export] public float Speed = 5.0f;
	[Export] public MeshInstance3D HeightLine { get; set; }
	[Export] public Decal ShadowDecal { get; set; }

	[Export] public RayCast3D HeightRay { get; set; }

	public static Player Instance { get; private set; }

	public const float JumpVelocity = 4.5f;
	private float _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
	private float _maxHealth;
	private Timer _spawnTimer;

	public static string State { get; private set; }

	public Player()
	{
		_maxHealth = Health;
		Title = "Player";
		State = "idle";
	}

	public override void _Ready()
	{
		base._Ready();
		HeightLine.Visible = false;
		if (Engine.IsEditorHint()) return;
		Instance = this;
		if (SaveManager.Instance.SaveFileExists())
		{
			Position = SaveManager.Instance.LoadPlayerPosition();
		}
		else
		{
			Position = new Vector3(0, 10, 0);
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
		UpdateStateString();
		ControlDecal();

		if (Health <= 0) Die();

		// store position in save manager
		SaveManager.Instance.State.Data.PlayerPosition = (Position.X, Position.Y, Position.Z);

		if (Input.IsActionJustPressed("save"))
		{
			SaveManager.Instance.SaveGame();
		}
	}


	public void ControlDecal()
	{
		// Raycast downwards to find the nearest surface
		if (!IsOnFloor())
		{
			HeightRay.ForceRaycastUpdate();
			if (HeightRay.IsColliding())
			{
				GodotObject collider = HeightRay.GetCollider();
				//DebugManager.Log($"Colllided with: {collider}");
				if (collider is StaticBody3D)
				{
					Vector3 collisionPoint = HeightRay.GetCollisionPoint();
					float distanceToSurface = HeightRay.GlobalPosition.DistanceTo(collisionPoint);
					//DebugManager.Log($"Distance to surface: {distanceToSurface}");
					float newsize = distanceToSurface + 0.1f;
					ShadowDecal.Size = new Vector3(ShadowDecal.Size.X, newsize, ShadowDecal.Size.Z); // Small buffer for projection
					ShadowDecal.Position = new Vector3(ShadowDecal.Position.X, -newsize / 2, ShadowDecal.Position.Z); // position under player
				}
			}
		}
	}


	public string GetStunTimeLeft()
	{
		return _stunTimer.TimeLeft.ToString();
	}

	private void UpdateStateString()
	{
		if (IsStunned())
		{
			State = "stunned";
		}
		else if (IsOnFloor())
		{
			State = "floor; ";
			if (Velocity.Length() < 0.1f)
				State += "idle";
			else State += "walking";
		}
		else
		{
			State = "air";
		}
	}

	private void Die()
	{
		HeightLine.Visible = false;
		// Implement death logic here
		DebugManager.Log($"{Title}: I'm dead!");
		QueueFree();
	}

	private void HandleMovement(double delta)
	{
		if (!IsOnFloor())
		{
			Velocity -= new Vector3(0f, _gravity * (float)delta, 0f);
			HeightLine.Visible = true;
			if (GlobalPosition.Y < -100f) Die(); // fall out of map
		}
		else HeightLine.Visible = false;

		if (!IsStunned())
		{ // input changes movement
			var velocity = Velocity;

			var ipt = Input.GetVector("move_left", "move_right", "move_up", "move_down");

			var direction = new Vector3(ipt.X, 0f, ipt.Y)
			.Rotated(Vector3.Up, CameraManager.Instance.Rotation.Y)
			.Normalized();

			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
			velocity.Y = Velocity[1];

			if (IsOnFloor()) Velocity = velocity;
			else
			{
				Velocity += new Vector3(direction.X, 0f, direction.Z);
				var _horzVelocity = new Vector3(Velocity[0], 0f, Velocity[2]);
				float len = _horzVelocity.Length();
				if (_horzVelocity.Length() > Speed)
				{
					var lerplen = Mathf.Lerp(len, Speed, 0.5f);
					_horzVelocity = _horzVelocity.Normalized() * lerplen;
				}
				Velocity = new Vector3(_horzVelocity[0], Velocity[1], _horzVelocity[2]);
			}
		}
		else
		{
			float friction = 0.98f;
			var _velocity = Velocity;
			if (IsOnFloor()) _velocity = new Vector3(_velocity.X * friction, Velocity[1], _velocity.Z * friction);
			Velocity = _velocity;
		}

		MoveAndSlide();
	}
}
