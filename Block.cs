using Godot;
using System;

public enum BlockHealth {
	Air = 0,
	Stone = 100,
	Dirt = 50,
	Grass = 50,
	Leaves = 10,
	Trunk = 100,
	Brick = 100,
	Lava = 999999
}

[Tool]
[GlobalClass]
public partial class Block : Resource
{
	[Export] public Texture2D Texture { get; set; }	
	[Export] public Texture2D TopTexture { get; set; }
	[Export] public Texture2D BottomTexture { get; set; }

	public Texture2D[] Textures => new Texture2D[] { Texture, TopTexture, BottomTexture };

	public int maxhealth = 1;

	public Block() { }
}
