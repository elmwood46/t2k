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

	Timer AoeLifeTimeTimer = new Timer();
	public double AoeLingerTime {get => AoeLifeTimeTimer.WaitTime; set => AoeLifeTimeTimer.WaitTime = value;}

	Timer explodeTimer = new Timer();

	// Timer expl

	public static Grenade Instantiate(){
		Grenade p = GD.Load<PackedScene>("res://spells/spell_objects/Grenade.tscn").Instantiate<Grenade>();
		return p;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AddChild(AoeLifeTimeTimer);
		AoeLifeTimeTimer.OneShot = true;
		AoeLifeTimeTimer.Timeout += QueueFree;

		AddChild(explodeTimer);
		explodeTimer.OneShot = true;
		explodeTimer.WaitTime = 1.5;
		explodeTimer.Start();
		aoeShape.Disabled = true;
		BodyEntered += HandleCollision;
	}

	private void HandleCollision(Node n){
		if(explodeTimer.TimeLeft != 0) return;
		if(n is StaticBody3D g){
			CallDeferred(nameof(Explode));
		}
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
		grenadeMaterial.AlbedoColor = new Godot.Color(color.X, color.Y, color.Z, 1);
		grendeVisual.MaterialOverride = grenadeMaterial;
		StandardMaterial3D areaMaterial = new StandardMaterial3D();
		areaMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		areaMaterial.AlbedoColor = new Godot.Color(color.X, color.Y, color.Z, 0.5f);
		areaVisual.MaterialOverride = areaMaterial;
	}

	// private void setColor(Vector3 color){

	// }



}
