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

	Timer timer;
	public double AoeLingerTime {get => timer.WaitTime; set => timer.WaitTime = value;}


	public static Grenade Instantiate(){
		Grenade p = GD.Load<PackedScene>("res://spells/spell_objects/Grenade.tscn").Instantiate<Grenade>();
		return p;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		timer = new Timer();
		AddChild(timer);
		timer.OneShot = true;
		timer.Timeout += QueueFree;

		aoeShape.Disabled = true;
		BodyEntered += (Node n) => {
			if(LinearVelocity.Y > 0) return;
			if(n is GridMap g){
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
		timer.Start();
	}

}
