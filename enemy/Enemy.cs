using Godot;
using System;

public partial class Enemy : Prop
{
	[Export]
	float health = 50;

	[Signal]
	public delegate void DiedEventHandler();

	public const float Speed = 4.0f;
	public const float JumpVelocity = 4.5f;
	private float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	[Export]
	private RayCast3D jumpCast;
    private float jumpRange = 1f;

    public override void _Ready()
    {
		base._Ready();
		Died += Die;	
    }

    public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += Vector3.Down * gravity * (float)delta;
		}


		Vector3 target = velocity.Normalized() * jumpRange;
		target.Y = 0;
		jumpCast.TargetPosition = target;
		jumpCast.ForceRaycastUpdate();
		// Handle Jump.
		if (jumpCast.IsColliding())
		{
			Node3D n = (Node3D)jumpCast.GetCollider();
			if(n is StaticBody3D) {
				velocity.Y = JumpVelocity;
			}
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector3 direction = GlobalPosition.DirectionTo(Player.Instance.GlobalPosition);
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	
	public void TakeDamage(float dmg){
		health -= dmg;
		if(health <= 0) EmitSignal(nameof(Died));
	}


	private void Die()
	{
		GamePoints.UpdatePoints(10); 
		QueueFree();
	}
}
