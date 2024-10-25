using Godot;
using System;
using System.Drawing;

public partial class Projectile : Area3D
{
	// return instance of this
	public static Projectile Instantiate(){
		Projectile p = GD.Load<PackedScene>("res://spells/spell_objects/Projectile.tscn").Instantiate<Projectile>();
		return p;
	}

	public float Range { get => shape.Radius; set => shape.Radius = value; }

    [Export]
	SphereShape3D shape;

	[Export]
	CollisionShape3D child;

	[Export]
	MeshInstance3D mesh;

	public Vector3 Velocity {get; set;}

	public override void _Ready()
	{
		child.Shape = shape;
		if(mesh.Mesh is SphereMesh m){
			m.Radius = shape.Radius /2;
			m.Height = shape.Radius;
		}
		Range = 0.1f;
	}

	public override void _Process(double delta)
	{
		Position += Velocity * (float) delta;
	}

	public void SetColor(Vector3 color){
		if(mesh.Mesh is SphereMesh m)
 			if(m.Material is StandardMaterial3D s)
				s.AlbedoColor = new Godot.Color(color.X, color.Y, color.Z, 1);
	}
}
