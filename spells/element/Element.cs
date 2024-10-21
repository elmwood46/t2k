using Godot;
using System;

public partial class Element : Node
{

	[Export]
	public int Damage {get; set;} 
	
	[Export]
	public Area3D Projectile {get; set;} 

	public void InflictDmg(Pawn pawn){
		
	}
	
}
