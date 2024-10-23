using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class Chunk : StaticBody3D
{
	[Export]
	public CollisionShape3D CollisionShape { get; set; }

	[Export]
	public MeshInstance3D MeshInstance { get; set; }

	public static Vector3I Dimensions = new Vector3I(16, 64, 16);

	private static readonly Vector3I[] _vertices = new Vector3I[]
	{
		new Vector3I(0, 0, 0),
		new Vector3I(1, 0, 0),
		new Vector3I(0, 1, 0),
		new Vector3I(1, 1, 0),
		new Vector3I(0, 0, 1),
		new Vector3I(1, 0, 1),
		new Vector3I(0, 1, 1),
		new Vector3I(1, 1, 1)
	};

	private static readonly int[] _top = new int[] { 2, 3, 7, 6 };
	private static readonly int[] _bottom = new int[] { 0, 4, 5, 1 };
	private static readonly int[] _left = new int[] { 6, 4, 0, 2 };
	private static readonly int[] _right = new int[] { 3, 1, 5, 7 };
	private static readonly int[] _back = new int[] { 7, 5, 4, 6 };
	private static readonly int[] _front = new int[] { 2, 0, 1, 3 };

	private ArrayMesh _arrayMesh = new ArrayMesh();
	private SurfaceTool _regularSurfaceTool = new();
	private SurfaceTool _lavaSurfaceTool = new();

	private Block[,,] _blocks = new Block[Dimensions.X, Dimensions.Y, Dimensions.Z];

	public Vector2I ChunkPosition { get; private set; }

	[Export]
	public FastNoiseLite Noise { get; set; }

	[Export]
	public FastNoiseLite WallNoise { get; set; }

	public void SetChunkPosition(Vector2I position)
	{
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
		CallDeferred(Node3D.MethodName.SetGlobalPosition, new Vector3(ChunkPosition.X * Dimensions.X, 0, ChunkPosition.Y * Dimensions.Z));

		Generate();
		Update();
	}

	public void Generate()
	{
		if (Engine.IsEditorHint()) return;

		//int playerChunkX = Mathf.FloorToInt(_playerPosition.X / Chunk.Dimensions.X);
		//int playerChunkZ = Mathf.FloorToInt(_playerPosition.Z / Chunk.Dimensions.Z);
		Vector2I chunkId = ChunkPosition;

		// check if chunk already exists
		if (SaveManager.Instance.LoadChunkOrNull(chunkId) is Block[,,] savedBlocks)
		{
			_blocks = savedBlocks;
			return;
		}

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		bool genWalls = rng.Randf() < 0.5f;

		// generate the _blocks[] array
		for (int x = 0; x < Dimensions.X; x++)
		{
			for (int y = 0; y < Dimensions.Y; y++)
			{
				for (int z = 0; z < Dimensions.Z; z++)
				{
					Block block;

					var globalBlockPosition = ChunkPosition * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z);
					var groundHeight = (int)(0.05f * Dimensions.Y + 4f*((Noise.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Y) + 1f) / 2f));

					// generating origin chunk - set player spawn positon
					if (chunkId == Vector2I.Zero && y==groundHeight && x==Dimensions.X/2 && z==Dimensions.Z/2) {
						genWalls = false;
						CallDeferred(nameof(SetPlayerSpawnY), (float)(groundHeight+2));
					}

					if (y == 0) {
						block = BlockManager.Instance.Lava;
					}
					else if (y < groundHeight / 2)
					{
						block = BlockManager.Instance.Stone;
					}
					else if (y < groundHeight)
					{
						block = BlockManager.Instance.Dirt;
					}
					else if (y == groundHeight)
					{
						block = BlockManager.Instance.Grass;

						// spawn a tree over a grass block
						int _margin = 2;
						if (!genWalls && chunkId != Vector2I.Zero && x > _margin && x < (Dimensions.X - _margin) && z > _margin && z < (Dimensions.Z - _margin)) // chunk margin of 2 blocks
						{
							float _xoffset = (float)(x-Dimensions.X/2);
							float _zoffset = (float)(z-Dimensions.Z/2);
							_xoffset *= _xoffset;
							_zoffset *= _zoffset;

							if (rng.Randf() < 0.05/(1+(Mathf.Sqrt(_xoffset+_zoffset)))) // spawn chance
							{ 
								GenTree tree = new GenTree();
								foreach (KeyValuePair<(int,int,int), Block> kvp in tree.Blocks)
								{
									Vector3I treeBlockPosition = new Vector3I(x + kvp.Key.Item1, y + kvp.Key.Item2, z + kvp.Key.Item3);

									if (treeBlockPosition.X < Dimensions.X && treeBlockPosition.X >= 0
									&&  treeBlockPosition.Y < Dimensions.Y && treeBlockPosition.Y >= 0
									&&  treeBlockPosition.Z < Dimensions.Z && treeBlockPosition.Z >= 0)
										_blocks[treeBlockPosition.X,treeBlockPosition.Y,treeBlockPosition.Z] = kvp.Value;
									else DebugManager.Log($"Tree block at {treeBlockPosition} was out of bounds");
								}
							}
						}
					}
					else
					{	
						// dont replace tree blocks if they exist
						if (!(_blocks[x,y,z]==BlockManager.Instance.Leaves||_blocks[x,y,z]==BlockManager.Instance.Trunk)) {
							// gen walls above ground
							if (genWalls && y < groundHeight + 3 && WallNoise.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Y) >= 0.95f)
							{
								block = BlockManager.Instance.Brick;
							}
							else
							{
								block = BlockManager.Instance.Air;
							}
						} else block = _blocks[x,y,z];
					}

					_blocks[x, y, z] = block;
				}
			}
		}
		SaveManager.Instance.SaveChunk(chunkId, _blocks);

	}

	private void SetPlayerSpawnY(float y) {
		Player.Instance.Position = new Vector3(0, y, 0);
	}

	public void Update()
	{
		_regularSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
		_lavaSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		for (int x = 0; x < Dimensions.X; x++)
		{
			for (int y = 0; y < Dimensions.Y; y++)
			{
				for (int z = 0; z < Dimensions.Z; z++)
				{
					CreateBlockMesh(new Vector3I(x, y, z));
				}
			}
		}

		_arrayMesh.ClearSurfaces();

		_regularSurfaceTool.SetMaterial(BlockManager.Instance.ChunkMaterial);
		var mesh = _regularSurfaceTool.Commit();
		var arrays = mesh.SurfaceGetArrays(0);
		_arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		_lavaSurfaceTool.SetMaterial(BlockManager.Instance.LavaShaderMaterial);
		var lavaMesh = _lavaSurfaceTool.Commit();
		var lavArrays = lavaMesh.SurfaceGetArrays(0);
		_arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, lavArrays);

		_arrayMesh.SurfaceSetMaterial(0, BlockManager.Instance.ChunkMaterial);
		_arrayMesh.SurfaceSetMaterial(1, BlockManager.Instance.LavaShaderMaterial);

		MeshInstance.Mesh = _arrayMesh;
		CollisionShape.Shape = MeshInstance.Mesh.CreateTrimeshShape();

		SaveManager.Instance.SaveChunk(ChunkPosition, _blocks); // Save the chunk after updating the _blocks[,,] and mesh
	}

	private void CreateBlockMesh(Vector3I blockPosition)
	{
		var block = _blocks[blockPosition.X, blockPosition.Y, blockPosition.Z];

		if (block == BlockManager.Instance.Air) return;

		SurfaceTool currentSurfaceTool = block == BlockManager.Instance.Lava ? _lavaSurfaceTool : _regularSurfaceTool;

		if (CheckTransparent(blockPosition + Vector3I.Up))
		{
			CreateFaceMesh(currentSurfaceTool, _top, blockPosition, block.TopTexture ?? block.Texture);
		}

		if (CheckTransparent(blockPosition + Vector3I.Down))
		{
			CreateFaceMesh(currentSurfaceTool, _bottom, blockPosition, block.BottomTexture ?? block.Texture);
		}

		if (CheckTransparent(blockPosition + Vector3I.Left))
		{
			CreateFaceMesh(currentSurfaceTool, _left, blockPosition, block.Texture);
		}

		if (CheckTransparent(blockPosition + Vector3I.Right))
		{
			CreateFaceMesh(currentSurfaceTool, _right, blockPosition, block.Texture);
		}

		if (CheckTransparent(blockPosition + Vector3I.Forward))
		{
			CreateFaceMesh(currentSurfaceTool, _front, blockPosition, block.Texture);
		}

		if (CheckTransparent(blockPosition + Vector3I.Back))
		{
			CreateFaceMesh(currentSurfaceTool, _back, blockPosition, block.Texture);
		}
	}

	private void CreateFaceMesh(SurfaceTool _surfaceTool, int[] face, Vector3I blockPosition, Texture2D texture)
	{
		var texturePosition = BlockManager.Instance.GetTextureAtlasPosition(texture);
		var textureAtlasSize = BlockManager.Instance.TextureAtlasSize;

		var uvOffset = texturePosition / textureAtlasSize;
		var uvWidth = 1f / textureAtlasSize.X;
		var uvHeight = 1f / textureAtlasSize.Y;

		var uvA = uvOffset + new Vector2(0, 0);
		var uvB = uvOffset + new Vector2(0, uvHeight);
		var uvC = uvOffset + new Vector2(uvWidth, uvHeight);
		var uvD = uvOffset + new Vector2(uvWidth, 0);

		var a = _vertices[face[0]] + blockPosition;
		var b = _vertices[face[1]] + blockPosition;
		var c = _vertices[face[2]] + blockPosition;
		var d = _vertices[face[3]] + blockPosition;

		var uvTriangle1 = new Vector2[] { uvA, uvB, uvC };
		var uvTriangle2 = new Vector2[] { uvA, uvC, uvD };

		var triangle1 = new Vector3[] { a, b, c };
		var triangle2 = new Vector3[] { a, c, d };

		var normal = ((Vector3)(c - a)).Cross(((Vector3)(b - a))).Normalized();
		var normals = new Vector3[] { normal, normal, normal };

		_surfaceTool.AddTriangleFan(triangle1, uvTriangle1, normals: normals);
		_surfaceTool.AddTriangleFan(triangle2, uvTriangle2, normals: normals);
	}

	private bool CheckTransparent(Vector3I blockPosition)
	{
		if (blockPosition.X < 0 || blockPosition.X >= Dimensions.X) return true;
		if (blockPosition.Y < 0 || blockPosition.Y >= Dimensions.Y) return true;
		if (blockPosition.Z < 0 || blockPosition.Z >= Dimensions.Z) return true;

		return _blocks[blockPosition.X, blockPosition.Y, blockPosition.Z] == BlockManager.Instance.Air;
	}

	public void SetBlock(Vector3I blockPosition, Block block)
	{
		_blocks[blockPosition.X, blockPosition.Y, blockPosition.Z] = block;
		Update();
	}

	public Block GetBlock(Vector3I blockPosition)
	{
		return _blocks[blockPosition.X, blockPosition.Y, blockPosition.Z];
	}
}
