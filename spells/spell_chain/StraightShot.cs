using Godot;
using System;
using System.Data;

public partial class StraightShot : SpellChainComponent
{
    public override string Description => "Shoots projectile in clicked direction when projectile hit an enemy applys damage and invokes next spell a collision point";
    public override Texture Icon => ImageTexture.CreateFromImage(Image.LoadFromFile("res://spells/spell_icons/StraightShotIcon.jpg"));

    private Element element;

    public StraightShot(Element e){
        element = e;
    }

    float speed = 10;


    public override void Invoke(CastPropertys cast)
    {
        if(element is null) throw new NoNullAllowedException("Element must be set");

        Projectile p = Projectile.Instantiate();
        cast.SceneReference.AddChild(p);
        p.Position = cast.Origin;
        p.Velocity = cast.Direction.Normalized() * speed;

        cast.CurrentSpellDepth++;
        p.BodyEntered += (Node3D a) => {
            if(a is DamageHitBox dhb){
                element.ApplyDamageProc(dhb);
                cast.Origin = a.Position; 
                Next?.Invoke(cast);
                p.QueueFree();
            }

            if(a is GridMap){
                GD.Print("hit grid");
                Next?.Invoke(cast);
                p.QueueFree();
            }
        };
    }
}
