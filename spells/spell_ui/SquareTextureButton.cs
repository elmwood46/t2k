using Godot;
using System;

[Tool]
public partial class SquareTextureButton : AspectRatioContainer
{
	public static SquareTextureButton Instantiate(){
		return GD.Load<PackedScene>("res://spells/spell_ui/SquareTextureButton.tscn").Instantiate<SquareTextureButton>();
	}

	[Export]
	public TextureButton Button {get; private set;}

	public override void _Ready()
	{
		OnButtonResized();
		if (Button != null){
			Button.Resized += OnButtonResized;
		}
	}

	private void OnButtonResized(){
		CustomMinimumSize = new Vector2(Button.GetRect().Size.X, CustomMinimumSize.Y); 
	}
	
}
