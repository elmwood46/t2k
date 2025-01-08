using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
	private int _width = 4;
	private int _y_width = 2;

	private Vector3 _playerPosition;
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
		for (int y = 0; y < _y_width; y++)
		{
			for (int x = 0; x < _width; x++)
			{
					for (int z=0; z < _width; z++) {
						var index = x + (z * _width) + (y * _width * _width);
						_chunks[index].SetChunkPosition(new Vector3I(x - halfWidth, y - halfywidth, z - halfWidth));
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
			Mathf.FloorToInt(globalPosition.Y / (float)Chunk.Dimensions.Y),
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
				Mathf.FloorToInt(globalPosition.Y / (float)Chunk.Dimensions.Y),
				Mathf.FloorToInt(globalPosition.Z / (float)Chunk.Dimensions.Z)
			);

			var chunkGlobalPosition = new Vector3I(
				chunkTilePosition.X * Chunk.Dimensions.X,
				chunkTilePosition.Y * Chunk.Dimensions.Y,
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

		lock (_positionToChunk)
		{
			foreach (var (chunkTilePosition, blockList) in chunkBlockMapping)
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

	private void UpdateChunkAsync(Vector3I newPosition, Chunk chunk)
	{
		var blocks = Chunk.Generate(newPosition);
		var mesh = Chunk.BuildChunkMesh(blocks);
		var hull = mesh.CreateTrimeshShape();
		//GD.Print($"Chunk {chunk} position set to {newPosition} on thread {Thread.CurrentThread.ManagedThreadId}");
		chunk.CallDeferred(nameof(Chunk.SetChunkPosition), newPosition, blocks, mesh, hull);
	}

	private async void ThreadProcess()
	{
		while (IsInstanceValid(this))
		{
			int playerChunkX, playerChunkY, playerChunkZ;
			lock(_playerPositionLock)
			{
				playerChunkX = Mathf.FloorToInt(_playerPosition.X / (Chunk.Dimensions.X*Chunk.VOXEL_SCALE));
				playerChunkY = Mathf.FloorToInt((_playerPosition.Y+Chunk.VOXEL_SCALE*Chunk.Dimensions.Y*0.5f) / (Chunk.Dimensions.Y*Chunk.SUBCHUNKS*Chunk.VOXEL_SCALE));
				playerChunkZ = Mathf.FloorToInt(_playerPosition.Z / (Chunk.Dimensions.Z*Chunk.VOXEL_SCALE));
			}

			foreach (var chunk in _chunks)
			{
				var chunkPosition = _chunkToPosition[chunk];

				var chunkX = chunkPosition.X;
				var chunkY = chunkPosition.Y;
				var chunkZ = chunkPosition.Z;

				var newChunkX = Mathf.PosMod(chunkX - playerChunkX + _width / 2, _width) + playerChunkX - _width / 2;
				var newChunkY = Mathf.PosMod(chunkY - playerChunkY + _y_width / 2, _y_width) + playerChunkY - _y_width / 2;
				var newChunkZ = Mathf.PosMod(chunkZ - playerChunkZ + _width / 2, _width) + playerChunkZ - _width / 2;

				if (newChunkX != chunkX || newChunkY != chunkY || newChunkZ != chunkZ)
				{
					lock(_positionToChunk)
					{
						if (_positionToChunk.ContainsKey(chunkPosition))
						{
							_positionToChunk.Remove(chunkPosition);
						}

						var newPosition = new Vector3I(newChunkX, newChunkY, newChunkZ);

						_chunkToPosition[chunk] = newPosition;
						_positionToChunk[newPosition] = chunk;
					}
					await Task.Run(() => UpdateChunkAsync(new Vector3I(newChunkX, newChunkY, newChunkZ), chunk));
					//Thread.Sleep(100);
				}
			}
			Thread.Sleep(100);
		}
	}
}