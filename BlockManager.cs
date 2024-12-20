using Godot;
using Godot.Collections;
using System;
using System.Linq;


[Tool]
public partial class BlockManager : Node
{

	[Export] public Block Air { get; set; }

	[Export] public Block Stone { get; set; }

	[Export] public Block Dirt { get; set; }

	[Export] public Block Grass { get; set; }

	[Export] public Block Leaves { get; set; }

	[Export] public Block Trunk { get; set; }

	[Export] public Block Brick { get; set; }

	[Export] public Block Lava { get; set; }

	private readonly Dictionary<Texture2D, Vector2I> _atlasLookup = new();

	private int _gridWidth = 8;
	private int _gridHeight;

	[Export] public ShaderMaterial LavaShaderMaterial { get; set; }

	public Vector2I BlockTextureSize { get; } = new(16, 16);

	public const int DamageTiers = 6;

	public Vector2 TextureAtlasSize { get; private set; }

	public static BlockManager Instance { get; private set; }

	public StandardMaterial3D ChunkMaterial { get; private set; }

	public static ImageTexture BlendImage(Image baseImage, Image overlayImage) {
		// Ensure both images are the same size
		if (baseImage.GetSize() != overlayImage.GetSize())
		{
			GD.PrintErr("Textures must have the same size to blend.");
			return null;
		}

 		// Lock all images for editing
		// Blend the images pixel by pixel
        Vector2 size = baseImage.GetSize();
		Image blendedImage = new Image();
        blendedImage = Image.CreateEmpty((int)size.X, (int)size.Y, false, Image.Format.Rgba8);
        blendedImage.Fill(Color.Color8(0, 0, 0, 0));

        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                Color baseColor = baseImage.GetPixel(x, y);
                Color overlayColor = overlayImage.GetPixel(x, y);
                Color blendedColor = (overlayColor.A < 0.0001f) ? baseColor : overlayColor;
                blendedImage.SetPixel(x, y, blendedColor);
            }
        }

        // Create a texture from the blended image
        return ImageTexture.CreateFromImage(blendedImage);
	}

	public override void _Ready()
	{
		Instance = this;

		var baseTextures = new Block[] { Air, Stone, Dirt, Grass, Leaves, Trunk, Brick, Lava }.SelectMany(block => block.Textures).Where(texture => texture != null).Distinct().ToArray();

		_gridWidth = baseTextures.Length; // make the grid width the number of blocks and the grid height will be number of damage tiers

        // Initialize the array with the number of cracked textures
        var crackTextures = new Texture2D[DamageTiers];
        // Load each texture
        for (int i = 0; i < crackTextures.Length; i++)
        {
            crackTextures[i] = GD.Load<Texture2D>($"res://BlockTextures/cracks/blockbreak_{i}.png");
        }

		var blockTextures = new Texture2D[baseTextures.Length * crackTextures.Length];
		for (int i=0; i< blockTextures.Length; i++)
		{
			if (i<baseTextures.Length)
			{
				blockTextures[i] = baseTextures[i];
				continue;
			}

			Image baseBlock = baseTextures[i % baseTextures.Length].GetImage();
			Image crackTexture = crackTextures[(i / baseTextures.Length)].GetImage();
			// Create a new image to store the blended result
			blockTextures[i] = BlendImage(baseBlock, crackTexture);
		}

		for (int i = 0; i < blockTextures.Length; i++)
		{
			var texture = blockTextures[i];
			_atlasLookup.Add(texture, new Vector2I(i % _gridWidth, Mathf.FloorToInt(i / _gridWidth)));
		}
		_gridHeight = Mathf.CeilToInt(blockTextures.Length / (float)_gridWidth);

		var image = Image.CreateEmpty(_gridWidth * BlockTextureSize.X, _gridHeight * BlockTextureSize.Y, false, Image.Format.Rgba8);

		for (var x = 0; x < _gridWidth; x++)
		{
			for (var y = 0; y < _gridHeight; y++)
			{
				var imgIndex = x + y * _gridWidth;

				if (imgIndex >= blockTextures.Length) continue;

				var currentImage = blockTextures[imgIndex].GetImage();
				currentImage.Convert(Image.Format.Rgba8);

				image.BlitRect(currentImage, new Rect2I(Vector2I.Zero, BlockTextureSize), new Vector2I(x, y) * BlockTextureSize);
			}
		}

		var textureAtlas = ImageTexture.CreateFromImage(image);

		ChunkMaterial = new()
		{
			AlbedoTexture = textureAtlas,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};

		TextureAtlasSize = new Vector2(_gridWidth, _gridHeight);

		 // Save the image to a file (PNG format)
		 /*
        string path = "user://example_image.png";
        var error = image.SavePng(path);
		if (error == Error.Ok) GD.Print($"Image saved successfully to {path}");
        else  GD.PrintErr($"Failed to save image: {error}");*/
        
		GD.Print($"Done loading {blockTextures.Length} images to make {_gridWidth} x {_gridHeight} atlas");
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
