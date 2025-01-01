using Godot;
using System;


// each block has 6 textures from 0-5
// order: bott 

[Tool]
[GlobalClass]
public partial class Block : Resource
{
	// block ID is a unique number between 0-1024
	[Export] public string Name { get; set; }
	[Export] public byte MaxHealth { get; set; }
	[Export] public Texture2D MidTexture { get; set; }	
	[Export] public Texture2D BottomTexture { get; set; }
	[Export] public Texture2D TopTexture { get; set; }

	[ExportCategory("Face Textures")]
	[Export] public Texture2D LeftTexture { get; set; }
	[Export] public Texture2D RightTexture { get; set; }
	[Export] public Texture2D BackTexture { get; set; }
	[Export] public Texture2D FrontTexture { get; set; }
	
	public Texture2D[] Textures {
		get => _textures;
		set {
			if (MidTexture!= null && BottomTexture!= null && TopTexture!=null) {
				_textures = new Texture2D[] { BottomTexture,TopTexture,MidTexture,MidTexture,MidTexture,MidTexture };
			}
			else if (MidTexture!= null) {
				_textures = new Texture2D[] { MidTexture,MidTexture,MidTexture,MidTexture,MidTexture,MidTexture };
			}
			else {
				_textures = new Texture2D[] { BottomTexture,TopTexture,LeftTexture,RightTexture,BackTexture,FrontTexture };
			}
		}
	}
	private Texture2D[] _textures;
}