using Godot;
using System;

public partial class SpellSplitter : SpellChainComponent
{
	int splitCount = 2;
    public override string Description => "spawns x of the next spell component in the chain";
    public override Texture Icon => ImageTexture.CreateFromImage(Image.LoadFromFile("res://spells/spell_icons/SpellSplitterIcon.jpg"));
    

    public override void Invoke(CastPropertys cast)
    {
        for(int i = 0; i < splitCount; i ++){
			Next?.Invoke(cast);
		}
    }
}
