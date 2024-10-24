using Godot;
using System;

public abstract partial class Element : Node
{
	public abstract int Dmg {get;}  
	public abstract float AoeLingerTime {get;}
    string description;
	public abstract string GetDescription();
	public abstract void ApplyDamageProc(DamageHitBox hitBox);
}
