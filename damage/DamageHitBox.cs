using Godot;
using System;

public partial class DamageHitBox : StaticBody3D
{
	[Export]
	Prop target;

	public void applyDamage(int dmg){
		// GD.Print("dmgInflict " + dmg);
		// target?.TakeDamage(dmg);
	}
}
