using Godot;
using System;

public partial class DamageHitBox : StaticBody3D
{
	[Export]
	Pawn target;

	public void applyDamage(int dmg){
		GD.Print("dmgInflict " + dmg);
		target?.TakeDamage(dmg);
	}
}
