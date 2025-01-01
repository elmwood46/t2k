using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

// note each block has 6 textures from 0-5
// order: bottom, top, left, right, back, front 

[Tool]
public partial class BlockManager : Node
{
	[Export] public Array<Block> Blocks { get; set; }

	private readonly System.Collections.Generic.Dictionary<Texture2D, Vector2I> _atlasLookup = new();

	private readonly System.Collections.Generic.Dictionary<string, int> _blockIdLookup = new();

	private const int ATLAS_WIDTH = 6; // gridwidth equivalent to number of faces (we store blocks vertically and faces horizontally)
	private int _atlas_height;           // number of blocks

	public Vector2I BlockTextureSize { get; } = new(16, 16);

	public Vector2 TextureAtlasSize { get; private set; }

	public static BlockManager Instance { get; private set; }

	public ShaderMaterial ChunkMaterial { get; private set; }

	public static int BlockID(string blockName) {
		return Instance._blockIdLookup[blockName];
	}

	public static bool IsEmpty(int blockID) {
		return blockID == Instance._blockIdLookup["Air"];
	}

	public override void _Ready()
	{
		Instance = this;
		if (Blocks == null) throw new Exception("Blockmanager failed to create texture atlas: blocks array is null.");
		_atlas_height = Blocks.Count;
		GD.Print($"Creating texture atlas for {_atlas_height} blocks");
		GD.Print(Blocks[0].Name);
		GD.Print(Blocks[0].Textures);
		var image = Image.CreateEmpty(ATLAS_WIDTH * BlockTextureSize.X, _atlas_height * BlockTextureSize.Y, false, Image.Format.Rgba8);

		//init block id lookup
		var textureCount = 0;
		for (int i = 0; i < Blocks.Count; i++) {
			GD.Print($"Block {Blocks[i].Name} has {Blocks[i].Textures.Length} textures");
			_blockIdLookup.Add(Blocks[i].Name, i);
			var textures = Blocks[i].Textures;
			GD.Print($"Block {Blocks[i].Name} has {textures.Length} textures");
			if (textures == null || textures.Length != 6) throw new Exception($"Block {Blocks[i].Name} has a wrongly sized texture array ({textures.Length}).");
			for (int j = 0; j < textures.Length; j++) {
				Texture2D texture = textures[j];
				if (texture != null) {
					_atlasLookup.TryAdd(texture, new Vector2I(j, i));
					textureCount++;
				}
				var imgIndex = i + j * ATLAS_WIDTH;
				if (imgIndex >= Blocks.Count) continue;
				var currentImage = texture?.GetImage();
				if (currentImage != null) {
					currentImage.Convert(Image.Format.Rgba8);
					image.BlitRect(currentImage, new Rect2I(Vector2I.Zero, BlockTextureSize), new Vector2I(j, i) * BlockTextureSize);
				}
			}
		}

		// fetch block textures
		var textureAtlas = ImageTexture.CreateFromImage(image);

		ChunkMaterial = GD.Load("res://shaders/chunk_shader.tres") as ShaderMaterial;
		//ChunkMaterial.SetShaderParameter("albedo_texture", GD.Load("res://BlockTextures/bricks.png") as Texture2D);
/*
		var ChunkMaterial = new()
		{
			AlbedoTexture = textureAtlas,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};*/

		TextureAtlasSize = new Vector2(ATLAS_WIDTH, _atlas_height);

		 // Save the image to a file (PNG format)
		 /*
        string path = "user://example_image.png";
        var error = image.SavePng(path);
		if (error == Error.Ok) GD.Print($"Image saved successfully to {path}");
        else  GD.PrintErr($"Failed to save image: {error}");*/
        
		GD.Print($"Done loading {textureCount} images to make {ATLAS_WIDTH} x {_atlas_height} atlas");
	}

	public Vector2I GetTextureAtlasPosition(Texture2D texture)
	{
		if (texture == null)
		{
			return Vector2I.Zero;
		}
		else
		{
			return _atlasLookup[texture];
		}
	}
}
