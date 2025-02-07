using System.Collections.Generic;
using Godot;

public partial class Explosion : Node3D
{
	[Export] public float ExplosionRadius {get;set;} = 3.0f;
	public const float DEFAULT_CHAR_BODY_MASS = 10.0f;
	[Export] public float Damage {get;set;} = 10.0f;
	[Export] public float ExplosionForce {get;set;} = 100.0f;
	[Export] public GpuParticles3D ExplosionParticles;
	[Export] public AudioStreamPlayer3D ExplosionSound;
	private AnimationPlayer _explosionAnimation;
	[Export] public Node3D ExplosionAnimationMeshes;
	[Export] public Area3D ExplosionCollisionArea;

	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
        CollisionShape3D collisionShape = new()
        {
            Shape = new SphereShape3D { Radius = ExplosionRadius }
        };
        ExplosionCollisionArea.AddChild(collisionShape);
		ExplosionAnimationMeshes.Visible = false;
		AddChild(ExplosionAnimationMeshes);
		foreach (var child in ExplosionAnimationMeshes.GetChildren())
		{
			if (child is AnimationPlayer a)
			{
				_explosionAnimation = a;
				break;
			}
		}

		// wait for 2 physics frames to finish
		// so overlapping bodies are calculated
		
        await ToSignal(GetTree(), "physics_frame");
		await ToSignal(GetTree(), "physics_frame");

		ChunkManager.DamageSphere(GlobalPosition, ExplosionRadius, (int)Damage, true);
		PushAwayObjects();
		Explode();
		
		// Delay collision checking by a frame
		/*
		GetTree().CreateTimer(0.05f).Timeout += () => {
			PushAwayObjects();
			Explode();
		};*/
	}



	private void PushAwayObjects() {
		Godot.Collections.Array<Node3D> _colliding_nodes = ExplosionCollisionArea.GetOverlappingBodies();

		foreach (Node3D node in _colliding_nodes) {
			GD.Print("body found: " + node.Name);
			var body_position = node.GlobalPosition;
			
			// calculate mass
			var mass = 1.0f;
			if (node is CharacterBody3D) {
				body_position.Y += 1.0f;
				mass = DEFAULT_CHAR_BODY_MASS; // characterBody3D has no mass so we set it here
			}
			else if (node is RigidBody3D rb) {
				//GD.Print(node.Name + " mass: " + rb.Mass);
				mass = Mathf.Max(0.01f,rb.Mass);
			}

			var force_dir = GlobalPosition.DirectionTo(body_position);
			var bodyDist = body_position.DistanceTo(GlobalPosition);
			var knockbackFromRadius = ExplosionForce
				* (1f - Mathf.Min(bodyDist/ExplosionRadius,1f))
				/ mass
				* force_dir;

			if (node is CharacterBody3D c) {
				/*GD.Print("PLAYER applying impulse: ", knockbackFromRadius);
				GD.Print("PLAYER body dist: ", bodyDist);
				GD.Print("PLAYER force dir: ", force_dir);*/
				c.Velocity += knockbackFromRadius;
			}
			else if (node is RigidBody3D rb) {
				/*GD.Print("RBODY applying impulse: ", knockbackFromRadius);
				GD.Print("RBODY body dist: ", bodyDist);
				GD.Print("RBODY force dir: ", force_dir);*/
				rb.ApplyImpulse(knockbackFromRadius);
			}

			if (node is PhysicsBody3D pb && pb.GetParent().GetParent() is DestructibleMesh mesh) {
				mesh.TakeDamage(ChunkManager.SphereDamageDropoff(GlobalPosition, body_position, Damage, ExplosionRadius), BlockDamageType.Physical);
			}

			if (node is IHurtable hurtable) {
				hurtable.TakeDamage(ChunkManager.SphereDamageDropoff(GlobalPosition, body_position, Damage, ExplosionRadius), BlockDamageType.Physical);
			}
		}
	}

	private void Explode() {
		ExplosionAnimationMeshes.Visible = true;
		_explosionAnimation.Play("explode");
		ExplosionSound.Play();
		ExplosionParticles.Emitting = true;

		// timer to destroy explosion after animation
        Timer _animation_timer = new()
        {
            WaitTime = _explosionAnimation.CurrentAnimationLength
        };
        _animation_timer.Timeout += () => {
			ExplosionParticles.Emitting = false;
			ExplosionAnimationMeshes.Visible = false;
			QueueFree();
		};
		AddChild(_animation_timer);
		_animation_timer.Start();
	}
}