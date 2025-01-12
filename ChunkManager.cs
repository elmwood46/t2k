using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

[Tool]
public partial class ChunkManager : Node
{
	public static ChunkManager Instance { get; private set; }

	private Dictionary<Chunk, Vector3I> _chunkToPosition = new();
	private Dictionary<Vector3I, Chunk> _positionToChunk = new();

	private List<Chunk> _chunks;

	[Export] public PackedScene ChunkScene { get; set; }

	// this is the number of chunks rendered in the x and z direction, centered around the player
	private int _width = 8;
	private int _y_width = 4;
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
    private static readonly int[,] AXIS = 
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
						_chunks[index].InitChunk(pos);
					}
				}
		}

		if (!Engine.IsEditorHint())
		{
			new Thread(new ThreadStart(ThreadProcess)).Start();
		}
	}

	public void UpdateChunkPosition(Chunk chunk, Vector3I currentPosition, Vector3I previousPosition)
	{
		if (_positionToChunk.TryGetValue(previousPosition, out var chunkAtPosition) && chunkAtPosition == chunk)
		{
			_positionToChunk.Remove(previousPosition);
		}

		_chunkToPosition[chunk] = currentPosition;
		_positionToChunk[currentPosition] = chunk;
	}

	public void SetBlock(Vector3I globalPosition, int block_type)
	{
		var chunkTilePosition = new Vector3I(
			Mathf.FloorToInt(globalPosition.X / (float)Chunk.Dimensions.X),
			Mathf.FloorToInt(globalPosition.Y / ((float)Chunk.Dimensions.Y*Chunk.SUBCHUNKS)),
			Mathf.FloorToInt(globalPosition.Z / (float)Chunk.Dimensions.Z)
		);
		lock (_positionToChunk)
		{
			if (_positionToChunk.TryGetValue(chunkTilePosition, out var chunk))
			{
				var v = (Vector3I)(globalPosition - chunk.GlobalPosition);
				var blockidx = v.X + v.Z * Chunk.Dimensions.X + v.Y * Chunk.Dimensions.X * Chunk.Dimensions.Z;
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
				Mathf.FloorToInt(globalPosition.X / (float)Chunk.Dimensions.X),
				Mathf.FloorToInt(globalPosition.Y / ((float)Chunk.Dimensions.Y*Chunk.SUBCHUNKS)),
				Mathf.FloorToInt(globalPosition.Z / (float)Chunk.Dimensions.Z)
			);

			var chunkGlobalPosition = new Vector3I(
				chunkTilePosition.X * Chunk.Dimensions.X,
				chunkTilePosition.Y * Chunk.Dimensions.Y*Chunk.SUBCHUNKS,
				chunkTilePosition.Z * Chunk.Dimensions.Z
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

	private Task UpdateChunkPositionAsync(Vector3I newPosition, Chunk chunk) {
							lock(_positionToChunk)
					{
						int[] blocks = Generate(newPosition);
		chunk.CallDeferred(nameof(Chunk.SetChunkPosition), newPosition, blocks);
					}
		return Task.CompletedTask;
	}

	private async void ThreadProcess()
	{
		while (IsInstanceValid(this))
		{
			int playerChunkX, playerChunkZ; //playerChunkY
			lock(_playerPositionLock)
			{
				playerChunkX = Mathf.FloorToInt(_playerPosition.X / (Chunk.Dimensions.X*Chunk.VOXEL_SCALE));
				//playerChunkY = Mathf.FloorToInt(_playerPosition.Y / (Chunk.Dimensions.Y*Chunk.SUBCHUNKS*Chunk.VOXEL_SCALE));
				//playerChunkY = Mathf.FloorToInt((_playerPosition.Y+Chunk.VOXEL_SCALE*Chunk.Dimensions.Y*Chunk.SUBCHUNKS*0.5f) / (Chunk.Dimensions.Y*Chunk.SUBCHUNKS*Chunk.VOXEL_SCALE));
				playerChunkZ = Mathf.FloorToInt(_playerPosition.Z / (Chunk.Dimensions.Z*Chunk.VOXEL_SCALE));
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

						//var blocks = Generate(newPosition);
						//var mesh = BuildChunkMesh(blocks);
						//var hull = mesh.CreateTrimeshShape();
					}
					await Task.Run(() => {UpdateChunkPositionAsync(newPosition, chunk);});
				}
				Thread.Sleep(1);
			}
			Thread.Sleep(100);
		}
	}
}