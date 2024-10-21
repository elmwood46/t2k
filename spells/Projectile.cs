using Godot;
using System;
using System.Drawing;

public partial class Projectile : Area3D
{
	// return instance of this
	public static Projectile Instantiate(){
		Projectile p = GD.Load<PackedScene>("res://spells/Projectile.tscn").Instantiate<Projectile>();
		return p;
	}

	public float Range { get => shape.Radius; set => shape.Radius = value; }

    [Export]
	SphereShape3D shape;

	[Export]
	CollisionShape3D child;

	public Vector3 Velocity {get; set;}

	public override void _Ready()
	{
		child.Shape = shape;
		Range = 0.1f;
	}

	public override void _Process(double delta)
	{
		Position += Velocity * (float) delta;
	}
}
