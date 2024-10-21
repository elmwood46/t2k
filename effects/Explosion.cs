using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

public partial class Explosion : Node3D
{
	const float GOLDEN_ANGLE_RAD = 2.3999632297f;
	
	[Export] public float ExplosionRadius {get;set;} = 5.0f;
	[Export] public float Damage {get;set;} = 10.0f;
	[Export] public float Knockback {get;set;} = 25.0f;
	[Export] public Timer Timer;
	[Export] public GpuParticles3D ExplosionParticles;
	[Export] public AudioStreamPlayer3D ExplosionSound;
	[Export] public AnimationPlayer ExplosionAnimation;
	[Export] public Node3D ExplosionAnimationMesh;

	const int num_points = 250;
	private RayCast3D[] _rays;
	private static Vector3[] _points = GetPoints();
	private static Vector3[] GetPoints() { // makes a fibonacci spiral of evenly spaced points around a sphere
		Vector3[] _pts = new Vector3[num_points];
		for (int i=0;i<num_points;i++) {
			float y = 1.0f - 2.0f*i / num_points;
			float r = Mathf.Sqrt(1.0f - y * y);
			float phi = i * GOLDEN_ANGLE_RAD;
			float x = Mathf.Cos(phi) * r;
			float z = Mathf.Sin(phi) * r;
			_pts[i] = new Vector3(x, y, z);
		}
		return _pts;
	}


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ExplosionAnimationMesh.Visible = false;
		_rays = new RayCast3D[num_points];
		if (Timer != null)
			Timer.Timeout += Explode;
		else GD.PrintErr($"Timer not set for explosion {Name}.");
		CreateRays();

		// debug - start timer
		Timer.Start();
	}

	public void CreateRays() {
		int i=0;
		foreach (Vector3 p in _points) {
			RayCast3D ray = new();
			ray.TargetPosition = p*ExplosionRadius;
			ray.Enabled = false;
			ray.ExcludeParent = true;
			ray.GlobalTransform = GlobalTransform;
			AddChild(ray);
			_rays[i] = ray;
			i++;
		}
	}

	private List<RayCast3D> GetCollidingRays() {
		List<RayCast3D> colliding_rays = new();
		for (int i=0;i<num_points;i++) {
			_rays[i].Enabled = true;
			_rays[i].ForceRaycastUpdate();
			if (_rays[i].IsColliding()) {
				colliding_rays.Add(_rays[i]);
			}
		}
		return colliding_rays;
	}

	private void Explode() {
		// GD.Print($"Explosion {Name}!");
		ExplosionAnimationMesh.Visible = true;
		ExplosionAnimation.Play("explode");
		ExplosionSound.Play();
		ExplosionParticles.Emitting = true;
		List<RayCast3D> colliding_rays = GetCollidingRays();
		foreach (RayCast3D ray in colliding_rays) {
			var collider = ray.GetCollider();
			if (collider is Pawn) {
				Pawn p = collider as Pawn;
				if (p.StunTimer.IsStopped()) {
					p.StunTimer.WaitTime = 1.0f;
					//p.StunTimer.Start();
				}
				p.TakeDamage(CalculateDamage(ray.GetCollisionPoint()));
				p.Velocity = CalculateKnockback(ray.GetCollisionPoint()) + new Vector3(0, 1.0f, 0); //add vertical to knockback
			} else if (collider is DynamicMap) {
				DynamicMap d = collider as DynamicMap;
				d.DamageTile(ray.GetCollisionPoint(), GlobalTransform.Origin, CalculateDamage(ray.GetCollisionPoint()));
			}
		}
		QueueFree();
	}

	private int CalculateDamage(Vector3 point) {
		float distance = GlobalTransform.Origin.DistanceTo(point);
		return Mathf.RoundToInt(Damage * (1.0f - (distance / ExplosionRadius)));
	}

	private Vector3 CalculateKnockback(Vector3 point) {
		Vector3 _k = GlobalTransform.Origin.DirectionTo(point).Normalized();
		float distance = GlobalTransform.Origin.DistanceTo(point);
		return Knockback * (1.0f - (distance / ExplosionRadius)) * _k;
	}
}