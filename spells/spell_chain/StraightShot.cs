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
        if(cast.CurrentSpellDepth == 0){
            Projectile p = Projectile.Instantiate();
            cast.SceneReference.AddChild(p);
            p.Position = cast.Origin;
            p.Velocity = cast.Direction.Normalized();
            p.AreaEntered += (Area3D a) => {

                GD.Print(a);
            };
        }
    }
}
