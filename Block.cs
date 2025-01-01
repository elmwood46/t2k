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
	[Export] public Texture2D MidTexture { get => _midTexture; set {_midTexture = value; SetTextures();} }
	private Texture2D _midTexture;
	[Export] public Texture2D BottomTexture { get => _bottomTexture; set {_bottomTexture = value; SetTextures();} }
	private Texture2D _bottomTexture;
	[Export] public Texture2D TopTexture { get => _topTexture; set {_topTexture = value; SetTextures();} }
	private Texture2D _topTexture;

	[ExportCategory("Face Textures")]
	[Export] public Texture2D LeftTexture { get => _leftTexture; set {_leftTexture = value; SetTextures();} }
	private Texture2D _leftTexture;
	[Export] public Texture2D RightTexture { get => _rightTexture; set {_rightTexture = value; SetTextures();} }
	private Texture2D _rightTexture;
	[Export] public Texture2D BackTexture { get => _backTexture; set {_backTexture = value; SetTextures();} }
	private Texture2D _backTexture;
	[Export] public Texture2D FrontTexture { get => _frontTexture; set {_frontTexture = value; SetTextures();} }
	private Texture2D _frontTexture;
	public Texture2D[] Textures {get => _textures; private set {_textures = value;}}
	private Texture2D[] _textures = new Texture2D[6];
	private void SetTextures() {
		if (MidTexture!= null && BottomTexture!= null && TopTexture!=null) {
			Textures = new Texture2D[] { BottomTexture,TopTexture,MidTexture,MidTexture,MidTexture,MidTexture };
		}
		else if (MidTexture!= null) {
			Textures = new Texture2D[] { MidTexture,MidTexture,MidTexture,MidTexture,MidTexture,MidTexture };
		}
		else {
			Textures = new Texture2D[] { BottomTexture,TopTexture,LeftTexture,RightTexture,BackTexture,FrontTexture };
		}
	}
}