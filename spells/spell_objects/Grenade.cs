using Godot;
using System;

public partial class Grenade : RigidBody3D
{

	[Export]
	Area3D aoeBody;

	[Export]
	CollisionShape3D aoeShape;

	[Signal]
	public delegate void ExplodedEventHandler(Area3D aoe); 

	[Export]
	MeshInstance3D grendeVisual;

	[Export]
	MeshInstance3D areaVisual;

	Timer AoeLifeTimeTimer;
	public double AoeLingerTime {get => AoeLifeTimeTimer.WaitTime; set => AoeLifeTimeTimer.WaitTime = value;}

	// Timer expl

	public static Grenade Instantiate(){
		Grenade p = GD.Load<PackedScene>("res://spells/spell_objects/Grenade.tscn").Instantiate<Grenade>();
		return p;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AoeLifeTimeTimer = new Timer();
		AddChild(AoeLifeTimeTimer);
		AoeLifeTimeTimer.OneShot = true;
		AoeLifeTimeTimer.Timeout += QueueFree;

		aoeShape.Disabled = true;
		BodyEntered += (Node n) => {
			
			if(LinearVelocity.Y > 0) return;
			if(n is StaticBody3D g){
				CallDeferred(nameof(Explode));
			}
		};
	}

	private void Explode(){
		Sleeping = true;
		Freeze = true;
		aoeShape.Disabled = false;
		aoeBody.GlobalPosition = GlobalPosition; 
		EmitSignal(nameof(Exploded), aoeBody);
		AoeLifeTimeTimer.Start();
	}

	public void SetColor(Vector3 color){
		StandardMaterial3D grenadeMaterial = new StandardMaterial3D();
		GD.Print(grenadeMaterial);
		grenadeMaterial.AlbedoColor = new Godot.Color(color.X, color.Y, color.Z, 1);
		grendeVisual.MaterialOverride = grenadeMaterial;

		StandardMaterial3D areaMaterial = new StandardMaterial3D();
		areaMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		areaMaterial.AlbedoColor = new Godot.Color(color.X, color.Y, color.Z, 0.5f);
		areaVisual.MaterialOverride = areaMaterial;

	}

}
