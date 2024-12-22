using System.Collections.Generic;
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
			if (node is Chunk) {
				Chunk c = node as Chunk;

				Dictionary<Vector3I,int> blockDamages = new();
				int r = Mathf.CeilToInt(ExplosionRadius);

				for (int x = -r; x <= r; x++)
				{
					for (int y = -r; y <= r; y++)
					{
						for (int z = -r; z <= r; z++)
						{
							Vector3I blockPosition = (Vector3I)GlobalPosition + new Vector3I(x, y, z);

							// Check if the block is within the sphere
							float dist = ((Vector3I)GlobalPosition).DistanceTo(blockPosition);
							if (dist <= r)
							{

								blockDamages[blockPosition] = (int)(Damage*(1.0-(dist/r)));
							}
						}
					}
				}

				ChunkManager.Instance.DamageBlocks(blockDamages);
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