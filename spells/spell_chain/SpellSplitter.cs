using Godot;
using System;

public partial class SpellSplitter : SpellChainComponent
{
	int splitCount = 2;
    float splitAngle = 45;
    public override string Description => "spawns x of the next spell component in the chain";
    public override Texture2D Icon => ImageTexture.CreateFromImage(Image.LoadFromFile("res://spells/spell_icons/SpellSplitterIcon.jpg"));
    

    public override void Invoke(CastPropertys cast)
    {
        GD.Print("splt");
        Vector3 v = cast.Direction.Rotated(new Vector3(0, 1, 0), Mathf.DegToRad(-(splitAngle - (splitAngle / 2)))); 
        for(int i = 0; i < splitCount; i ++){
			Vector3 val = v.Rotated(new Vector3(0, 1, 0), Mathf.DegToRad(splitAngle * i)); 
            cast.Direction = val;
            Next?.Invoke(cast);
		}
    }
}
