using Godot;
using System;

public abstract partial class Element : Node
{
	public abstract int Dmg {get;}  
	public abstract float AoeLingerTime {get;}
	// public abstract Material Material {get;}

	public abstract Vector3 Color {get;}

    string description;
	public abstract string GetDescription();
	public abstract void ApplyDamageProc(DamageHitBox hitBox);
}
