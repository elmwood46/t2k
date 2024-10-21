using Godot;
using System;

public partial class StraightShot : SpellChainComponent
{
    string description = "Shoots projectile in clicked direction when projectile hit an enemy applys damage and invokes next spell";
    public override string getDescription() { return description; }

    Element element;
    Vector3 velocity;
    float speed;

    public override void Invoke(CastPropertys cast)
    {
        Projectile p = Projectile.Instantiate();
        cast.SceneReference.AddChild(p);
        p.Position = cast.Origin;
        p.Velocity = cast.Direction.Normalized();

        cast.CurrentSpellDepth++;
        p.BodyEntered += (Node3D a) => {
            if(a is GridMap){
                GD.Print("hit griddy");
                Next?.Invoke(cast);
                p.QueueFree();
            }
        };
    }
}
