using Godot;
using System;

public partial class Enemy : Prop
{
	public int Health {get; set;} = 20;

	public const float Speed = 4.0f;
	public const float JumpVelocity = 4.5f;
	private float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	[Export]
	private RayCast3D jumpCast;
    private float jumpRange = 1f;

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += Vector3.Down * gravity * (float)delta;
		}


		jumpCast.TargetPosition = velocity.Normalized() * jumpRange;
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

}
