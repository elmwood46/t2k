using Godot;
using System;

public partial class EarthElement : Element
{
    string description = "high single instance of dmg";

    public override int Dmg => 6;
    public override float AoeLingerTime => 1;

    public override string GetDescription()
    {
		return description;
	}

    public override void ApplyDamageProc(DamageHitBox hitBox)
    {
        hitBox.applyDamage(Dmg);
    }

}
