using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

// note each block has 6 textures from 0-5
// order: bottom, top, left, right, back, front 
// NOTE that the "back" texture faces in the -z direction (which is "forward" in Godot's physics)
// the "front" texture faces in the +z direction (which is "back" in Godot's physics)
// this is because we construct the blocks in "chunk space" and don't transform the textures to godot's physics space

[Tool]
public partial class BlockManager : Node
{

	// note the code assumes that block 0 is the empty (air) block
	// but this has to be set manually
	[Export] public Array<Block> Blocks { get; set; }

	private readonly System.Collections.Generic.Dictionary<Texture2D, int> _texarraylookup = new();

	private readonly System.Collections.Generic.Dictionary<string, int> _blockIdLookup = new();

	private const int ATLAS_WIDTH = 6; // gridwidth equivalent to number of faces (we store blocks vertically and faces horizontally)
	private int _atlas_height;           // number of blocks

	public Vector2I BlockTextureSize { get; } = new(16, 16);

	public Vector2 TextureAtlasSize { get; private set; }

	public static BlockManager Instance { get; private set; }

	public ShaderMaterial ChunkMaterial { get; private set; }

	public ShaderMaterial LavaShader { get; private set; }

	public Texture2DArray TextureArray = new();

	public int LavaBlockId {get; private set;}

	public static int BlockID(string blockName) {
		return Instance._blockIdLookup[blockName];
	}

	public static string BlockName(int blockID) {
		return Instance.Blocks[blockID].Name;
	}

	public static int[] BlockTextureArrayPositions(int blockID) {
		return Instance.Blocks[blockID].BakedTextureArrayPositions;
	}

	public static int InitBlockInfo(int blockID) {
		return blockID << 15 | Instance.Blocks[blockID].MaxHealth;
	}

	private int[] GetBlockTextureArrayPositions(int blockID) {
		var texarray = Instance.Blocks[blockID].Textures;
		var result = new int[texarray.Length];
		for (int i=0; i<texarray.Length; i++) {
			result[i] = _texarraylookup[texarray[i]];
			//GD.Print($"Block {Blocks[blockID].Name} texture {i} is at array position {result[i]}");
		}
		for (int i=0; i< 6; i++) GD.Print(result[i]);
		return result;
	}

	public override void _Ready()
	{
		Instance = this;

		LavaShader = GD.Load("res://shaders/LavaShader.tres") as ShaderMaterial;

		if (Blocks[0].Name != "Air") throw new Exception("Blockmanager blocks array was set up incorrectly: block 0 must be the air block.");	
		var enumerable = Blocks.Select(block => {block.SetTextures(); return block;}).SelectMany(block => block.Textures).Where(texture => texture != null).Distinct();
		var blockTextures = enumerable.ToArray();
        var blockImages = new Array<Image> (enumerable.Select(texture =>
				{
					var image = texture.GetImage(); // Create an Image object from the texture
					return image;
				})
			);

		//var tex_array = new Texture2DArray();
		for (int i = 0; i < blockTextures.Length; i++)
		{
			var texture = blockTextures[i];
			_texarraylookup.Add(texture, i);    // map texture to position in texture array
		}
		TextureArray.CreateFromImages(blockImages);

		// get the ordered texture array positions for each block's texture
		// and store them in the block
		for (int i=0;i<Blocks.Count;i++) {
			_blockIdLookup.Add(Blocks[i].Name, i);
			if (Blocks[i].Name == "Lava") LavaBlockId = i;
			Instance.Blocks[i].BakedTextureArrayPositions = GetBlockTextureArrayPositions(i);
		}

		ChunkMaterial = GD.Load("res://shaders/chunk_uv_shader.tres") as ShaderMaterial;
		ChunkMaterial.SetShaderParameter("_albedo", TextureArray);

		 // Save the image to a file (PNG format)
        /*
		GD.Print($"Block textures: {blockTextures.Length}");
		GD.Print($"Block images: {blockImages.Count}");
		GD.Print($"Texture array size: {TextureArray.GetLayers()}");
		GD.Print($"Done loading {blockTextures.Length} images to make {TextureArray.GetLayers()} sized texture array");
		*/
		
		/*for (int i=0; i< tex_array.GetLayers(); i++) {
			GD.Print(tex_array.GetLayerData(i));
			string path = $"user://texture_for_array_layer_{i}.png";
			var error = tex_array.GetLayerData(i).SavePng(path);
			if (error == Error.Ok) GD.Print($"Image saved successfully to {path}");
			else  GD.PrintErr($"Failed to save image: {error}");
		}*/
	}
}
