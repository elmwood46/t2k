using Godot;
using System;
using System.Data;
using System.Linq;


public partial class GrenadeSpell : SpellChainComponent
{
    public override string Description => "throws grenade of element that explodes when hit floor of enemy leaving a aoe of element";

    public override Texture2D Icon => ImageTexture.CreateFromImage(Image.LoadFromFile("res://spells/spell_icons/GrenadeSpellIcon.jpg"));

    Element element;

    public GrenadeSpell(Element e){
        element = e;
    }

    public override void Invoke(CastPropertys cast)
    {
        GD.Print("gr");
        if(element is null) throw new NoNullAllowedException("Element must be set");

		Grenade g = Grenade.Instantiate();
        cast.SceneReference.AddChild(g);
        g.Position = cast.Origin;
        g.AoeLingerTime = element.AoeLingerTime;
        g.SetColor(element.Color);
        Vector3 up = new Vector3(0, 10, 0);
        g.LinearVelocity = up + cast.Direction;

        g.Exploded += (Area3D aoe) => {
            cast.Origin = g.Position;
            Next?.Invoke(cast);
            aoe.GetOverlappingBodies().ToList<Node3D>().ForEach((e) => {
                if(e is DamageHitBox hb){
                    element.ApplyDamageProc(hb);
                }
            });
            aoe.BodyEntered += (Node3D n) => {if(n is DamageHitBox hb){element.ApplyDamageProc(hb);}};
        };
    }
}
