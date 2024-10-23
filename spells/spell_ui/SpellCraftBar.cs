using Godot;
using System;
using System.Collections.Generic;

public partial class SpellCraftBar : Control
{
	[Export]
	Container bar;

	private int? selectedIndex = null;
	private int? mouseOverIndex = null;

	[Signal]
	public delegate void SpellsSwappedEventHandler(int from, int to);

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		bar.Resized += () => bar.CustomMinimumSize = new Vector2(
			bar.CustomMinimumSize.Y * SpellManager.MAX_SPELL_LENGTH,
			bar.CustomMinimumSize.Y
		);

		for(int i = 0; i < SpellManager.MAX_SPELL_LENGTH; i++){
			SquareTextureButton btn = SquareTextureButton.Instantiate();
			bar.AddChild(btn);
			btn.Button.TextureNormal = ImageTexture.CreateFromImage(Image.LoadFromFile("res://spells/spell_icons/GrenadeSpellIcon.jpg"));
			btn.Button.IgnoreTextureSize = true;
			btn.Button.StretchMode = TextureButton.StretchModeEnum.Scale;

			int v = i;
			// btn.Button.ButtonDown += () => {
			// 	GD.Print("down " + v);
			// 	selectedIndex = v;
			// };

			btn.Button.ButtonUp	+= () => {
				// GD.Print("up " + v);
				selectedIndex = v;
				if(selectedIndex is null) return;
				if(mouseOverIndex is null) return;
				if(selectedIndex == mouseOverIndex) return;
				EmitSignal(nameof(SpellsSwapped), selectedIndex.Value, mouseOverIndex.Value);
			};

			btn.Button.MouseEntered += () => {
				// GD.Print("ent " + v);
				mouseOverIndex = v;
			};

			btn.Button.MouseExited += () => {
				// GD.Print("ext " + v);
				mouseOverIndex = null;
			};
		}
	}


	public void UpdateBar(List<SpellChainComponent> spellChain){
		for(int i = 0; i < spellChain.Count; i++){
			Texture t = spellChain[i].Icon;
			
		}
	}

	// public 
}
