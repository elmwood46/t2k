using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

public partial class ChunkManager : Node
{
	public static ChunkManager Instance { get; private set; }
	private Dictionary<Chunk, Vector3I> _chunkToPosition = new();
	private Dictionary<Vector3I, Chunk> _positionToChunk = new();

	public ConcurrentDictionary<Vector3I, int[]> ChunkCache = new();
	public ConcurrentDictionary<Vector3I, ChunkMeshData> ChunkMeshDataCache = new();

	private List<Chunk> _chunks;

	[Export] public PackedScene ChunkScene { get; set; }

	// this is the number of chunks rendered in the x and z direction, centered around the player
	private int _width = 6;
	private int _y_width = 6;
	public int Width
	{
		get => _width;
		set
		{
			_width = value;
		}
	}
	public int YWidth
	{
		get => _y_width;
		set
		{
			_y_width = value;

		}
	}

	public static readonly Noise CELLNOISE = new FastNoiseLite(){NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular
	, CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Manhattan,
	FractalType = FastNoiseLite.FractalTypeEnum.Fbm, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue};

	
	public static readonly Noise WHITENOISE = new FastNoiseLite(){
		NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
		CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Manhattan,
		Frequency = 0.01f,
		FractalOctaves = 0,
		FractalLacunarity = 0f,
		FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
		CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.CellValue,
		CellularJitter = 0f
	};

	public static readonly Noise NOISE = new FastNoiseLite();



	public const float VOXEL_SCALE = 1f; // chunk space is integer based, so this is the scale of each voxel (and the chunk) in world space
    public const float INV_VOXEL_SCALE = 1/VOXEL_SCALE;

    // chunk size is 30, padded chunk size is 32. Can't be increased easily because it uses binary UINT32 to do face culling
	public const int CHUNK_SIZE = 30; // the chunk size is 62, padded chunk size is 64, // must match size in compute shader
    public const int CHUNKSQ = CHUNK_SIZE*CHUNK_SIZE;
    public const int CSP = CHUNK_SIZE+2;
    public const int CSP2 = CSP*CSP; // squared padded chunk size
    public const int CSP3 = CSP2*CSP; // cubed padded chunk size
	public static readonly Vector3I Dimensions = new(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);

    // in the Generate() method, noise >= this value doesnt generate blocks
    public const float NOISE_CUTOFF = 0.2f;

    public const int SUBCHUNKS = 1; // each one is an extra 32x32 chunk in the vertical y direction

	public static readonly Vector3I[] NeighbourVectors = new Vector3I[]
	{
		new(0, 0, 1),
		new(0, 0, -1),
		new(1, 0, 0),
		new(-1, 0, 0),
	};

	// surface tools

	// vertices of a cube
    private static readonly Vector3I[] CUBE_VERTS = 
        {
            new(0, 0, 0),
			new(1, 0, 0),
            new(0, 1, 0),
            new(1, 1, 0),
            new(0, 0, 1),
            new(1, 0, 1),
            new(0, 1, 1),
            new(1, 1, 1)
        };

    // vertices for a square face of the above, cube depending on axis
    // axis has 2 entries for each coordinate - y, x, z and alternates between -/+
    // axis 0 = down, 1 = up, 2 = right, 3 = left, 4 = front (-z is front in godot), 5 = back
    private static readonly int[,] CUBE_AXIS = 
        {
            {0, 4, 5, 1}, // bottom
            {2, 3, 7, 6}, // top
            {6, 4, 0, 2}, // left
            {3, 1, 5, 7}, // right
            {2, 0, 1, 3}, // front
            {7, 5, 4, 6}  // back
        };

	private Godot.Vector3 _playerPosition;
	private object _playerPositionLock = new();

	private object _cantorSetLock = new();

	#region init
	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		Instance = this;

		_chunks = new List<Chunk>(_width * _width * _y_width);
		//_chunks =  GetParent().GetChildren().Where(child => child is Chunk).Select(child => child as Chunk).ToList()
		
		for (int i = 0; i < _width * _width * _y_width; i++)
		{
			var chunk = ChunkScene.Instantiate<Chunk>();
			GetParent().CallDeferred(Node.MethodName.AddChild, chunk);
			_chunks.Add(chunk);
		}

		GD.Print("chunks: ", _chunks.Count);

		InitChunks();
	}

	public void InitChunks()
	{
		//Vector2I playerChunk;
		//playerChunk = !SaveManager.Instance.SaveFileExists() ? new Vector2I(0,0)
		// = new Vector2I(Mathf.FloorToInt(Player.Instance.Position.X),Mathf.FloorToInt(Player.Instance.Position.Z));

		var halfWidth = Mathf.FloorToInt(_width / 2f);
		var halfywidth = Mathf.FloorToInt(_y_width / 2f);

		for (int x = 0; x < _width; x++)
			{
				for (int z=0; z < _width; z++) {
					for (int y = 0; y < _y_width; y++)
					{
						var index = x + (z * _width) + (y * _width * _width);
						var pos = new Vector3I(x - halfWidth, y, z - halfWidth);
						//var blocks = Generate(pos);
						//var mesh = BuildChunkMesh(blocks);
						//var hull = mesh.CreateTrimeshShape();
						//UpdateChunkBlockData(pos);
						//UpdateChunkMeshData(pos);
						var chunk = _chunks[index];
						chunk.SetChunkPosition(pos);
                    	InitChunkData(pos);
					}
				}
		}

		foreach (var chunk in _chunks)
		{
			chunk.UpdateChunkPosition(chunk.ChunkPosition);
		}

		if (!Engine.IsEditorHint())
		{
			new Thread(new ThreadStart(ThreadProcess)).Start();
		}
	}
	#endregion


	public static void InitChunkData(Vector3I position)
	{
        UpdateChunkBlockData(position);
        UpdateChunkMeshData(position);
	}

	#region manipulate blocks

	public void SetBlock(Vector3I globalPosition, int block_type)
	{
		var chunkTilePosition = new Vector3I(
			Mathf.FloorToInt(globalPosition.X / (float)Dimensions.X),
			Mathf.FloorToInt(globalPosition.Y / ((float)Dimensions.Y*SUBCHUNKS)),
			Mathf.FloorToInt(globalPosition.Z / (float)Dimensions.Z)
		);
		lock (_positionToChunk)
		{
			if (_positionToChunk.TryGetValue(chunkTilePosition, out var chunk))
			{
				var v = (Vector3I)(globalPosition - chunk.GlobalPosition);
				var blockidx = v.X + v.Z * Dimensions.X + v.Y * Dimensions.X * Dimensions.Z;
				chunk.SetBlock(blockidx, block_type);
			}
		}
	}

	public void DamageBlocks(Dictionary<Vector3I, int> blockAndDamage)
	{
		var chunkBlockMapping = new Dictionary<Vector3I, List<(Vector3I, int)>>();

		foreach (var (globalPosition, damage) in blockAndDamage)
		{
			var chunkTilePosition = new Vector3I(
				Mathf.FloorToInt(globalPosition.X / (float)Dimensions.X),
				Mathf.FloorToInt(globalPosition.Y / ((float)Dimensions.Y*SUBCHUNKS)),
				Mathf.FloorToInt(globalPosition.Z / (float)Dimensions.Z)
			);

			var chunkGlobalPosition = new Vector3I(
				chunkTilePosition.X * Dimensions.X,
				chunkTilePosition.Y * Dimensions.Y*SUBCHUNKS,
				chunkTilePosition.Z * Dimensions.Z
			);

			var relativePosition = globalPosition - chunkGlobalPosition;

			if (!chunkBlockMapping.TryGetValue(chunkTilePosition, out var blockList))
			{
				blockList = new List<(Vector3I, int)>();
				chunkBlockMapping[chunkTilePosition] = blockList;
			}

			blockList.Add((relativePosition, damage));
		}

		foreach (var (chunkTilePosition, blockList) in chunkBlockMapping)
		{
			lock (_positionToChunk)
			{
				if (_positionToChunk.TryGetValue(chunkTilePosition, out var chunk))
				{
					chunk.DamageBlocks(blockList);
				}
			}
		}
	}
	#endregion

	public override void _PhysicsProcess(double delta)
	{
		if (!Engine.IsEditorHint())
		{
			lock (_playerPositionLock)
			{
				_playerPosition = Player.Instance.GlobalPosition;
			}
		}
	}

	#region updates

	public void UpdateChunkPosition(Chunk chunk, Vector3I currentPosition, Vector3I previousPosition)
	{
		if (_positionToChunk.TryGetValue(previousPosition, out var chunkAtPosition) && chunkAtPosition == chunk)
		{
			_positionToChunk.Remove(previousPosition);
		}

		_chunkToPosition[chunk] = currentPosition;
		_positionToChunk[currentPosition] = chunk;
	}


	public static void UpdateChunkBlockData(Vector3I chunkPosition, int[] blockData = null) {
		Instance.Generate(chunkPosition);
		if (blockData != null) {
			if (Instance.ChunkMeshDataCache.TryRemove(chunkPosition, out _)) {
				Instance.ChunkCache.TryAdd(chunkPosition,blockData);
			}
		}
	}

	public static void UpdateChunkMeshData(Vector3I chunkPosition)
	{
		if (Instance.ChunkCache.TryGetValue(chunkPosition, out var cachedBlocks)) {
			Instance.ChunkMeshDataCache.TryRemove(chunkPosition, out _);
			Instance.ChunkMeshDataCache.TryAdd(chunkPosition, BuildChunkMesh(cachedBlocks));
		}
		else {
			GD.Print("Tried to update chunk mesh data without block data.");
		}
	}

	public static int[] GetChunkBlockData(Vector3I chunkPosition)
	{
		if (Instance.ChunkCache.TryGetValue(chunkPosition, out var blockData))
		{
			return blockData;
		}
		else
		{
			throw new Exception("Tried to get chunk block data that doesn't exist.");
		}
	}

	public static ChunkMeshData GetChunkMeshData(Vector3I chunkPosition)
	{
		if (Instance.ChunkMeshDataCache.TryGetValue(chunkPosition, out var meshData))
		{
			return meshData;
		}
		else
		{
			throw new Exception("Tried to get chunk mesh data that doesn't exist.");
		}
	}

	private Task ThreadedChunkPosChange(Vector3I newPosition, Chunk chunk) {
		lock(_positionToChunk)
		{
			UpdateChunkBlockData(newPosition);
			UpdateChunkMeshData(newPosition);
			chunk.CallDeferred(nameof(Chunk.UpdateChunkPosition), newPosition);
		}
		return Task.CompletedTask;
	}
	#endregion

	#region thread process
	private async void ThreadProcess()
	{
		while (IsInstanceValid(this))
		{
			int playerChunkX, playerChunkZ; //playerChunkY

			//Godot.Vector3 player_glob_pos;

			lock(_playerPositionLock)
			{
				playerChunkX = Mathf.FloorToInt(_playerPosition.X / (Dimensions.X*VOXEL_SCALE));
				//playerChunkY = Mathf.FloorToInt(_playerPosition.Y / (Dimensions.Y*SUBCHUNKS*Chunk.VOXEL_SCALE));
				//playerChunkY = Mathf.FloorToInt((_playerPosition.Y+Chunk.VOXEL_SCALE*Dimensions.Y*SUBCHUNKS*0.5f) / (Dimensions.Y*SUBCHUNKS*Chunk.VOXEL_SCALE));
				playerChunkZ = Mathf.FloorToInt(_playerPosition.Z / (Dimensions.Z*VOXEL_SCALE));
				
				//player_glob_pos = _playerPosition;
			}

			foreach (var chunk in _chunks)
			{
				var chunkPosition = _chunkToPosition[chunk];

				var chunkX = chunkPosition.X;
				var chunkY = chunkPosition.Y;
				var chunkZ = chunkPosition.Z;

				var newChunkX = Mathf.PosMod(chunkX - playerChunkX + _width / 2, _width) + playerChunkX - _width / 2;
				var newChunkY = chunkY;//Mathf.PosMod(chunkY - playerChunkY + _y_width / 2, _y_width) + playerChunkY - _y_width / 2;
				var newChunkZ = Mathf.PosMod(chunkZ - playerChunkZ + _width / 2, _width) + playerChunkZ - _width / 2;

				if (newChunkX != chunkX || newChunkY != chunkY || newChunkZ != chunkZ)
				{
					var newPosition = new Vector3I(newChunkX, newChunkY, newChunkZ);
					lock(_positionToChunk)
					{
						if (_positionToChunk.ContainsKey(chunkPosition))
						{
							_positionToChunk.Remove(chunkPosition);
						}
						_chunkToPosition[chunk] = newPosition;
						_positionToChunk[newPosition] = chunk;
					}
					await Task.Run(() => {ThreadedChunkPosChange(newPosition, chunk);});
				}
				Thread.Sleep(1);
			}
			Thread.Sleep(100);
		}
	}
	#endregion
}