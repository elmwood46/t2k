using Godot;

public partial class Explosion : Node3D
{
	[Export] public float ExplosionRadius {get;set;} = 3.0f;
	[Export] public float Damage {get;set;} = 10.0f;
	[Export] public float Knockback {get;set;} = 10.0f;
	[Export] public GpuParticles3D ExplosionParticles;
	[Export] public AudioStreamPlayer3D ExplosionSound;
	[Export] public AnimationPlayer ExplosionAnimation;
	[Export] public Node3D ExplosionAnimationMeshes;
	[Export] public Area3D ExplosionCollisionArea;

	private Godot.Collections.Array<Node3D> _colliding_nodes = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        CollisionShape3D collisionShape = new()
        {
            Shape = new SphereShape3D { Radius = ExplosionRadius }
        };
        ExplosionCollisionArea.AddChild(collisionShape);
		DebugManager.Log($"Monitoring collision for {ExplosionCollisionArea}: {ExplosionCollisionArea.Monitoring }");
		ExplosionAnimationMeshes.Visible = false;

		// Delay collision checking by a frame
		GetTree().CreateTimer(0.05f).Timeout += () => {
			_colliding_nodes = ExplosionCollisionArea.GetOverlappingBodies();
			Explode();
		};
	}

	private void Explode() {
		_colliding_nodes = ExplosionCollisionArea.GetOverlappingBodies();
		DebugManager.Log($"Colliding Nodes {_colliding_nodes}");
		ExplosionAnimationMeshes.Visible = true;
		ExplosionAnimation.Play("explode");
		ExplosionSound.Play();
		ExplosionParticles.Emitting = true;
		
		DebugManager.Log($"Calculating collision for {_colliding_nodes.Count} nodes");
		foreach (Node3D node in _colliding_nodes) {
			if (node is Prop) {
				Prop p = node as Prop;
				DebugManager.Log($"prop collided: {p.Title}");

				var point = p.GlobalPosition;
				var _k = GlobalPosition.DirectionTo(point+ new Vector3(0f, 1.0f, 0f)).Normalized();//add vertical to knockback
				var distance = GlobalPosition.DistanceTo(point);

				if (!p.IsStunned()) {
					double stunTime = 1.0d-(double)(distance / ExplosionRadius);
					if (stunTime >= 0.1d) p.Stun(stunTime);
				}
				//p.TakeDamage(CalculateDamage(ray.GetCollisionPoint()));
				var knockback = Knockback * (1.0f - (distance / ExplosionRadius)) * _k; 
				p.Velocity = knockback;
			} else if (node is Chunk) {
				Chunk c = node as Chunk;
				//d.DamageTile(ray.GetCollisionPoint(), GlobalTransform.Origin, CalculateDamage(ray.GetCollisionPoint()));
			}
		}

		// timer to destroy explosion after animation
        Timer _animation_timer = new()
        {
            WaitTime = ExplosionAnimation.CurrentAnimationLength
        };
        _animation_timer.Timeout += () => {
			ExplosionParticles.Emitting = false;
			ExplosionAnimationMeshes.Visible = false;
			QueueFree();
		};
		AddChild(_animation_timer);
		_animation_timer.Start();
	}

	private int CalculateDamage(Vector3 point) {
		float distance = GlobalTransform.Origin.DistanceTo(point);
		return Mathf.RoundToInt(Damage * (1.0f - (distance / ExplosionRadius)));
	}

	private Vector3 CalculateKnockback(Vector3 point) {
		Vector3 _k = GlobalPosition.DirectionTo(point).Normalized();
		float distance = GlobalPosition.DistanceTo(point);
		return Knockback * (1.0f - (distance / ExplosionRadius)) * _k;
	}
}