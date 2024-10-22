using Godot;
using System;

public partial class FireElement : Element
{
    private readonly double TIME_BETWEEN_PROC = 0.5f;
    private readonly int PROC_COUNT = 5;

    string description = "low damage but procs multiple times";

    public override int Dmg => 1;

    public override void ApplyDamageProc(DamageHitBox hitBox)
    {
        GD.Print("fp: start");
        Timer t = new Timer();
        hitBox.AddChild(t);
        t.OneShot = false;
        t.WaitTime = TIME_BETWEEN_PROC;
        
        int procsLeftCount = PROC_COUNT;
        t.Timeout += () => {
            hitBox.applyDamage(Dmg);
            procsLeftCount--;
            if(procsLeftCount <= 0){
                t.Stop();
                t.QueueFree();
            }
        };
        t.Start();
    }

    public override string GetDescription()
    {
        return description;
    }

}
