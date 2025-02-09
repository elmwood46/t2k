using System.Collections.Generic;
using Godot;

public partial class Explosion : Node3D
{
	public const float DEFAULT_CHAR_BODY_MASS = 10.0f;
	[Export] public float ExplosionRadius {get;set;} = 3.0f;
	[Export] public float Damage {get;set;} = 10.0f;
	[Export] public float ExplosionForce {get;set;} = 100.0f;
	[Export] public Node3D ExplosionVfxScene;
	private AnimationPlayer _explosionAnimation;
	private Area3D _explosionCollisionArea;

	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
		_explosionCollisionArea = new Area3D();
		AddChild(_explosionCollisionArea);
        CollisionShape3D collisionShape = new()
        {
            Shape = new SphereShape3D { Radius = ExplosionRadius }
        };
        _explosionCollisionArea.AddChild(collisionShape);
		ExplosionVfxScene.Visible = false;
		foreach (var child in ExplosionVfxScene.GetChildren())
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
		PlayExplosionAnimation();
	}

	private void PushAwayObjects() {
		Godot.Collections.Array<Node3D> _colliding_nodes = _explosionCollisionArea.GetOverlappingBodies();

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
				mass = Mathf.Max(0.01f,rb.Mass);
			}

			var force_dir = GlobalPosition.DirectionTo(body_position);
			var bodyDist = body_position.DistanceTo(GlobalPosition);
			var knockbackFromRadius = ExplosionForce
				* (1f - Mathf.Min(bodyDist/ExplosionRadius,1f))
				/ mass
				* force_dir;

			if (node is CharacterBody3D c) {
				c.Velocity += knockbackFromRadius;
			}
			else if (node is RigidBody3D rb) {
				rb.ApplyImpulse(knockbackFromRadius);
			}

			if (node is PhysicsBody3D pb && pb.GetParent().GetParent() is DestructibleMesh mesh) {
				mesh.TakeDamage(ChunkManager.SphereDamageDropoff(GlobalPosition,((Node3D) mesh.IntactScene.GetChild(0)).GlobalPosition, Damage, ExplosionRadius), BlockDamageType.Fire);
			}

			if (node is IHurtable hurtable) {
				hurtable.TakeDamage(ChunkManager.SphereDamageDropoff(GlobalPosition, body_position, Damage, ExplosionRadius), BlockDamageType.Fire);
			}
		}
	}

	private void PlayExplosionAnimation() {
		ExplosionVfxScene.Visible = true;
		_explosionAnimation.Play("init");

		// timer to destroy explosion after animation
        var _animation_timer = new Timer()
        {
            WaitTime = _explosionAnimation.CurrentAnimationLength
        };
        _animation_timer.Timeout += () => {
			ExplosionVfxScene.Visible = false;
			QueueFree();
		};
		AddChild(_animation_timer);
		_animation_timer.Start();
	}
}