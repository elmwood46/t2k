using Godot;
using System;
using System.Data;

public partial class StraightShot : SpellChainComponent
{
    public override string Description => "Shoots projectile in clicked direction when projectile hit an enemy applys damage and invokes next spell a collision point";
    public override Texture2D Icon => ImageTexture.CreateFromImage(Image.LoadFromFile("res://spells/spell_icons/StraightShotIcon.jpg"));

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
        p.GlobalPosition = cast.Origin;
        p.Velocity = cast.Direction.Normalized() * speed;

        // cast.CurrentSpellDepth++;
        p.BodyEntered += (Node3D a) => {
            // GD.Print(a);
            // if(a.IsInGroup("World")) GD.Print("yep");
            if(a is DamageHitBox dhb){
                element.ApplyDamageProc(dhb);
                cast.Origin = p.GlobalPosition; 
                Next?.Invoke(cast);
                p.QueueFree();
            }else if(a is StaticBody3D){
                GD.Print("hit grid");
                cast.Origin = p.Position; 
                cast.Direction = -cast.Direction;
                Next?.Invoke(cast);
                p.QueueFree();
            }

        };
    }
}
