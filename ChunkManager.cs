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
	public ConcurrentDictionary<Vector3I, int[]> BLOCKCACHE = new();
	public ConcurrentDictionary<Vector3I, ChunkMeshData> MESHCACHE = new();

	// deferred mesh updates also holds a list of which blocks were filled in the chunk
	// this allows for more efficient processing of sloped blocks when we mesh the chunk
	public ConcurrentDictionary<Vector3I, List<Vector3I>> DeferredMeshUpdates = new();

	private List<Chunk> _chunks;
	[Export] public PackedScene ChunkScene { get; set; }
	// this is the number of chunks rendered in the x and z direction, centered around the player
	// the "render distance"
	private int _width = 14;
	private int _y_width = 4;
	public const float VOXEL_SCALE = 0.5f; // chunk space is integer based, so this is the scale of each voxel (and the chunk) in world space

	// a chunk 32x32x32 blocks, and 1 integer for each block holds packed block data
	// block type from 0-15, damage bits go from 16-23, slope bits go from 24-31
	public const int BLOCK_DAMAGE_BITS_OFFSET = 16;
	public const int BLOCK_SLOPE_BITS_OFFSET = 24;
	private Godot.Vector3 _playerPosition;
	private object _playerPositionLock = new();

	private static readonly Vector3I[] VECTOR_NEIGHBOUR_SET = {
		Vector3I.Down,
		Vector3I.Up,
		Vector3I.Left,
		Vector3I.Right,
		Vector3I.Forward,
		Vector3I.Back,
		Vector3I.Forward+Vector3I.Left,
		Vector3I.Forward+Vector3I.Right,
		Vector3I.Back+Vector3I.Left,
		Vector3I.Back+Vector3I.Right
	};

	#region init
	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		BLOCKCACHE.Clear();
		MESHCACHE.Clear();

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

	public async void InitChunks()
	{
		//Vector2I playerChunk;
		//playerChunk = !SaveManager.Instance.SaveFileExists() ? new Vector2I(0,0)
		// = new Vector2I(Mathf.FloorToInt(Player.Instance.Position.X),Mathf.FloorToInt(Player.Instance.Position.Z));

		var halfWidth = Mathf.FloorToInt(_width / 2f);
		var halfywidth = Mathf.FloorToInt(_y_width / 2f);
		var tasks = new List<Task>();

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

					tasks.Add(Task.Run(() =>
					{
						SetBlocksAndDeferMeshUpdates(pos);
						chunk.CallDeferred(nameof(chunk.SetChunkPosition),pos);
						return Task.CompletedTask;
					}));

					//await Task.Run(()=>{SetBlocksAndDeferMeshUpdates(pos);});
					//chunk.UpdateChunkPosition(pos);
				}
			}
		}

		await Task.WhenAll(tasks);

		tasks.Clear();
		foreach (var chunk_mesh_id in Instance.DeferredMeshUpdates.Keys) {
			tasks.Add(Task.Run(() => {
				TryUpdateChunkMeshData(chunk_mesh_id);
				return Task.CompletedTask;
			}));
		}
		await Task.WhenAll(tasks);

		foreach (var chunk in _chunks)
		{
			chunk.CallDeferred(nameof(chunk.Update));
		}

		Instance.DeferredMeshUpdates.Clear();

		if (!Engine.IsEditorHint())
		{
			new Thread(new ThreadStart(ThreadProcess)).Start();
		}
	}
	#endregion

	#region change blocks

	// DEBUG setting blocks, untested
	public static void TrySetBlock(Vector3I globalPosition, int block_type)
	{
		var chunkTilePosition = GlobalPositionToChunkPosition(globalPosition);
		if (Instance.BLOCKCACHE.TryGetValue(chunkTilePosition, out var _blocks)) {
			var blockpos = GlobalPositionToLocalBlockPosition(globalPosition);
			var _blockidx = BlockIndex(blockpos);
			_blocks[_blockidx] = RepackBlockType(_blocks[_blockidx], block_type);

			Instance.BLOCKCACHE[chunkTilePosition] = _blocks;
			Instance.MESHCACHE[chunkTilePosition] = BuildChunkMesh(chunkTilePosition);
			lock (Instance._positionToChunk) {
				if (Instance._positionToChunk.TryGetValue(chunkTilePosition, out var chunk)) {
					chunk.CallDeferred(nameof(chunk.Update));
				}
			}
		} else GD.Print($"Tried to SET block at global position {globalPosition} but block data was not found in dictionary.");
	}

	async public static void DamageBlocks(Dictionary<Vector3I, int> blockAndDamage)
	{
		var chunkBlockMapping = new Dictionary<Vector3I, List<(Vector3I, int)>>();
		var playerBlockPosition = new Vector3I();
		var tasks = new List<Task>();
		var chunkDestroyedBlocksLists = new Dictionary<Vector3I,Dictionary<Vector3I,int>>();
		var updateNeighbourChunks = new HashSet<Vector3I>();
		var slopeUpdateSet = new Dictionary<Vector3I,List<Vector3I>>();

		foreach (var (globalPosition, damage) in blockAndDamage)
		{
			var chunkTilePosition = GlobalPositionToChunkPosition(globalPosition);

			if (!Instance.BLOCKCACHE.ContainsKey(chunkTilePosition)) continue;

			var relativePosition = GlobalPositionToLocalBlockPosition(globalPosition);

			if (!chunkBlockMapping.TryGetValue(chunkTilePosition, out var blockList))
			{
				blockList = new List<(Vector3I, int)>();
				chunkBlockMapping[chunkTilePosition] = blockList;
			}

			blockList.Add((relativePosition, damage));
		}

		lock(Instance._playerPositionLock) {
			playerBlockPosition = GlobalPositionToLocalBlockPosition(Instance._playerPosition);
		}

		// need to update neighbour chunks if a block is destroyed in this chunk, or else the neighbour mesh can be exposed
		foreach (var (chunkTilePosition, blockList) in chunkBlockMapping)
		{
			// output dictionary of block positions where particles need to be spawned (for destroyed blocks) and textures for spawned particles
			var _blocks = TryGetChunkBlockData(chunkTilePosition);
			if (_blocks == null) continue;

			// array of tuples with block global position as Item1 and damage as Item2
			foreach ((var blockToDamage, var blockDamageAmount) in blockList)
			{				
				var block_idx = BlockIndex(blockToDamage);
				var blockinfo = _blocks[block_idx];

				//GD.Print("checking if block empty: ", BlockManager.BlockName(blockid));

				if (IsBlockEmpty(blockinfo) || IsBlockInvincible(blockinfo)) continue; // don't damage air blocks or invincible blocks

				// increase block damage percentage
				var block_damaged = (float)GetBlockDamageInteger(blockinfo);
				block_damaged += blockDamageAmount*GetBlockFragility(blockinfo);
				var dam_rounded = Mathf.RoundToInt(block_damaged);

				// DEBUG add physical damage for all attacks
				var packedDamageType = GetBlockDamageTypeFlag(blockinfo)|PackDamageFlag(BlockDamageType.Physical);

				if (dam_rounded >= BlockManager.BLOCK_BREAK_DAMAGE_THRESHOLD)
				{
					blockinfo = RepackDamageData(blockinfo, packedDamageType, BlockManager.BLOCK_BREAK_DAMAGE_THRESHOLD);

					// go from padded chunk position to chunk position
					if (!chunkDestroyedBlocksLists.TryGetValue(chunkTilePosition, out var particle_spawn_list))
					{
						particle_spawn_list = new Dictionary<Vector3I, int>();
						chunkDestroyedBlocksLists[chunkTilePosition] = particle_spawn_list;
					}
					particle_spawn_list[blockToDamage] = blockinfo;              
					_blocks[block_idx] = 0;
				}
				else
				{
					_blocks[block_idx] = RepackDamageData(blockinfo, packedDamageType, dam_rounded);
				}
			}

			TryUpdateOrGenerateChunkBlockData(chunkTilePosition,_blocks);

			// update mesh and collision shape
			// recalculate mesh slopes
			if (chunkDestroyedBlocksLists.TryGetValue(chunkTilePosition, out var spawnList)) {
				// collect neighbour positions for updating slopes at the border of destroyed blocks
				foreach (var key in spawnList.Keys) {
					foreach(var v in VECTOR_NEIGHBOUR_SET) { // add neighbour block to slope update list if it's not already in the list
						var neighbour = key + v;

						// update neighbourning chunks if a block was destroyed in this chunk (being in particle_spawn_lists means a block was destroyed)
						// don't update neighbour chunk if chunkDestroyedBlocksLists already contains it, because it will be updated in a future iteration of this loop
						var dx = neighbour.X > CHUNK_SIZE ? 1 : neighbour.X < 1 ? -1 : 0;
						var dy = neighbour.Y > CHUNK_SIZE ? 1 : neighbour.Y < 1 ? -1 : 0;
						var dz = neighbour.Z > CHUNK_SIZE ? 1 : neighbour.Z < 1 ? -1 : 0;
						var delta = new Vector3I(dx,dy,dz);
						if (dx!=0 || dy!=0 || dz!=0)
						{
							updateNeighbourChunks.Add(chunkTilePosition + delta);
						}

						if (spawnList.ContainsKey(neighbour)) continue;
						if (playerBlockPosition == neighbour) continue;
						if (BlockHasNeighbor(chunkTilePosition,key,v)) {
							if (!slopeUpdateSet.TryGetValue(chunkTilePosition, out var set)) {
								set = new();
								slopeUpdateSet[chunkTilePosition] = set;
							}
							slopeUpdateSet[chunkTilePosition].Add(neighbour);
						}
					}
				}

				tasks.Add(Task.Run(()=>
				{
					//GD.Print("updating single where blocks were broken ",chunkTilePosition);
					Instance.BLOCKCACHE[chunkTilePosition] = _blocks;
					return Task.CompletedTask;
				}));
			}
			else
			{
				//updateNeighbourChunks.Add(chunkTilePosition);
				
				tasks.Add(Task.Run(()=>
				{
					//GD.Print("updating single chunk, no damages ",chunkTilePosition);
					Instance.BLOCKCACHE[chunkTilePosition] = _blocks;
					return Task.CompletedTask;
				}));
			}
		}
		await Task.WhenAll(tasks);

		// update any neighborning chunks which now have exposed faces, and were not included in previous pass
		var neighboursToUpdate = updateNeighbourChunks.Where(chunkTilePosition => chunkTilePosition.Y < Instance._y_width && chunkTilePosition.Y >= 0 && !chunkDestroyedBlocksLists.ContainsKey(chunkTilePosition));
		foreach (var chunkTilePosition in neighboursToUpdate.Union(chunkBlockMapping.Keys))
		{
			tasks.Add(Task.Run(()=>
			{
				//GD.Print("updating neighbour chunk ",chunkTilePosition);
				if (slopeUpdateSet.TryGetValue(chunkTilePosition, out var slopeUpdateList)) {
					Instance.MESHCACHE[chunkTilePosition] = BuildChunkMesh(chunkTilePosition, slopeUpdateList);
				} else {
					Instance.MESHCACHE[chunkTilePosition] = BuildChunkMesh(chunkTilePosition);
				}
				TryUpdateChunkMeshData(chunkTilePosition);
				return Task.CompletedTask;
			}));
		}
		await Task.WhenAll(tasks);

		foreach (var chunkTilePosition in neighboursToUpdate.Union(chunkBlockMapping.Keys))
		{
			lock (Instance._positionToChunk)
			{
				if (Instance._positionToChunk.TryGetValue(chunkTilePosition, out var chunk))
				{
					chunk.CallDeferred(nameof(chunk.Update));
				}
			}
		}


		// update chunk tile positions and spawn rigid bodies
		foreach ((var chunkTilePosition, var particle_spawn_list) in chunkDestroyedBlocksLists)
		{
			lock (Instance._positionToChunk)
			{
				if (Instance._positionToChunk.TryGetValue(chunkTilePosition, out var chunk))
				{
					chunk.SpawnBlockParticles(particle_spawn_list, playerBlockPosition);
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
				//var chunkpos = GlobalPositionToChunkPosition(_playerPosition);
				_playerPosition = Player.Instance.GlobalPosition;
				/*if (GlobalPositionToChunkPosition(_playerPosition) != chunkpos)
				{
					GD.Print("player chunk position: ", GlobalPositionToChunkPosition(_playerPosition));
				}*/
			}
		}
	}

	#region updates

	public static void UpdateChunkPosition(Chunk chunk, Vector3I currentPosition, Vector3I previousPosition)
	{
		if (Instance._positionToChunk.TryGetValue(previousPosition, out var chunkAtPosition) && chunkAtPosition == chunk)
		{
			Instance._positionToChunk.Remove(previousPosition);
		}

		Instance._chunkToPosition[chunk] = currentPosition;
		Instance._positionToChunk[currentPosition] = chunk;
	}

	// called from the thread when a chunk needs to rebuild its mesh or is generating a new chunk
	// also when initializing chunks at start
	public static void SetBlocksAndDeferMeshUpdates(Vector3I chunkPosition, int[] blockData = null)
	{
		Instance.DeferredMeshUpdates.TryAdd(chunkPosition, new());
        TryUpdateOrGenerateChunkBlockData(chunkPosition,blockData);
	}

	public static void TryUpdateOrGenerateChunkBlockData(Vector3I chunkPosition, int[] updateBlockDataWith = null) {
		if (!Instance.BLOCKCACHE.TryGetValue(chunkPosition, out _)) {
			Generate(chunkPosition);
		} 
		else
		{
			// CantorPairing tells us whether the Generate() method was run or not for this chunk
			// when a chunk is generating, it may also check or set blocks in its neighbourning chunks before Generate() has been run on those chunks
			if (!CantorPairing.Contains(chunkPosition)) Generate(chunkPosition);
		}
		if (updateBlockDataWith != null) {
			Instance.BLOCKCACHE[chunkPosition] = updateBlockDataWith;
		}
	}

	public static void TryUpdateChunkMeshData(Vector3I chunkPosition)
	{
		if (Instance.BLOCKCACHE.TryGetValue(chunkPosition, out _)) {
			Instance.MESHCACHE[chunkPosition] = BuildChunkMesh(chunkPosition);
		}
	}

	public static int[] TryGetChunkBlockData(Vector3I chunkPosition)
	{
		if (Instance.BLOCKCACHE.TryGetValue(chunkPosition, out var blockData))
		{
			return blockData;
		}
		else
		{
			return null;
			throw new Exception("Tried to get chunk block data that doesn't exist.");
		}
	}

	public static ChunkMeshData TryGetChunkMeshData(Vector3I chunkPosition)
	{
		if (Instance.MESHCACHE.TryGetValue(chunkPosition, out var meshData))
		{
			return meshData;
		}
		return null;
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

			var tasks = new List<Task>();
			var newPositions = new Dictionary<Chunk,Vector3I>();
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

					newPositions.Add(chunk,newPosition);

					tasks.Add(Task.Run(() => {
						if (!CantorPairing.Contains(newPosition)) {
							SetBlocksAndDeferMeshUpdates(newPosition);
						}
						return Task.CompletedTask;
					}));

					Thread.Sleep(3);
				}
				//Thread.Sleep(1);
			}
			await Task.WhenAll(tasks);

			tasks.Clear();
			foreach (var pos in Instance.DeferredMeshUpdates.Keys) {
				tasks.Add(Task.Run(() => {
					TryUpdateChunkMeshData(pos);
					return Task.CompletedTask;
				}));
				Thread.Sleep(3);
			}
			await Task.WhenAll(tasks);

			// update chunks which need a deferred mesh update and aren't covered by the new positions
			lock (_positionToChunk) {
				foreach (var pos in  Instance.DeferredMeshUpdates.Keys.Except(newPositions.Values)) {
					if (_positionToChunk.ContainsKey(pos)) {
						_positionToChunk[pos].CallDeferred(nameof(Chunk.Update));
						Thread.Sleep(10);
					}
				}
			}
			Instance.DeferredMeshUpdates.Clear();

			// update chunks which changed position
			foreach ((var chunk, var pos) in newPositions) {
				chunk.CallDeferred(nameof(Chunk.UpdateChunkPosition), pos);
				Thread.Sleep(10);
			}

			Thread.Sleep(100);
		}
	}
	#endregion


    #region static block info

    public static int PackAllBlockInfo(int blockType, int damageType, int damageAmount, int slopeType, int slopeRotation, int blockflip) {
        return PackBlockType(blockType) | PackDamageData(damageType, damageAmount)<<BLOCK_DAMAGE_BITS_OFFSET| PackSlopeData(slopeType, slopeRotation, blockflip)<<BLOCK_SLOPE_BITS_OFFSET;
    }

    public static int PackBlockType(int blockType) {
        return blockType;
    }

    public static int PackSlopeData(int slopeType, int slopeRotation, int slopeFlip) {
        return slopeType | slopeRotation << 2 | slopeFlip << 4;
    }

    public static int PackDamageData(int damageTypeFlag, int damageAmount) {
        return (damageTypeFlag<<5) | damageAmount;
    }

	public static int PackDamageFlag(BlockDamageType damageType) {
		return damageType switch  {
            BlockDamageType.Physical => 1,
            BlockDamageType.Fire => 1<<1,
            _ => 1<<2
        };
	}

    public static int RepackBlockType(int blockInfo, int blockType) {
        return (blockInfo & ~0xffff) | blockType;
    }

    public static int RepackDamageData(int blockInfo, int damageTypeFlag, int damageAmount) {
        return RepackDamageData(blockInfo,PackDamageData(damageTypeFlag, damageAmount));
    }

	public static int RepackDamageData(int blockinfo, int packedDamageData) {
		return (blockinfo & ~(0xff<<BLOCK_DAMAGE_BITS_OFFSET)) | (packedDamageData<<BLOCK_DAMAGE_BITS_OFFSET);
	}

    public static int RepackSlopeData(int blockInfo, int slopeType, int slopeRotation, int slopeflip) {
        return RepackSlopeData(blockInfo,PackSlopeData(slopeType, slopeRotation, slopeflip));
    }

	public static int RepackSlopeData(int blockinfo, int packedSlopeData) {
		return (blockinfo & ~(0xff<<BLOCK_SLOPE_BITS_OFFSET)) | (packedSlopeData<<BLOCK_SLOPE_BITS_OFFSET);
	}

    public static int GetBlockID(int blockInfo) {
        return blockInfo & 0xffff;
    }

    public static int GetBlockDamageData(int blockInfo) {
        return (blockInfo>>BLOCK_DAMAGE_BITS_OFFSET) & 0xff;
    }

    public static int GetBlockDamageInteger(int blockInfo) {
        return GetBlockDamageData(blockInfo)&0x1f;
    }

    public static int GetBlockDamageTypeFlag(int blockInfo) {
        return GetBlockDamageData(blockInfo) >> 5;
    }

    public static int GetBlockSlopeData(int blockInfo) {
        return (blockInfo >> BLOCK_SLOPE_BITS_OFFSET)&0xff;
    }

    public static int GetBlockSlopeType(int blockInfo) {
        return GetBlockSlopeData(blockInfo) & 0b11;
    }

    public static float GetBlockSlopeRotation(int blockInfo) {
        return GetBlockSlopeRotationBits(blockInfo)*Mathf.Pi/2;
    }

	public static int GetBlockSlopeRotationBits(int blockInfo) {
		return (GetBlockSlopeData(blockInfo) >> 2) & 0b11;
	}

	public static bool GetBlockSlopeFlip(int blockinfo) {
		return ((GetBlockSlopeData(blockinfo) >> 4)&1) == 1;
	}
    public static BlockSpecies GetBlockSpecies(int blockinfo) {
        return BlockManager.Instance.Blocks[GetBlockID(blockinfo)].Species;
    }

    public static float GetBlockFragility(int blockinfo) {
        return BlockManager.Instance.Blocks[GetBlockID(blockinfo)].Fragility;
    }

    public static bool IsBlockEmpty(int blockInfo) {
        return GetBlockID(blockInfo) == 0;
    }

    public static bool IsBlockSloped(int blockInfo) {
        return GetBlockSlopeType(blockInfo) != (int)SlopeType.None;
    }

    public static bool IsBlockInvincible(int blockInfo) {
        return IsBlockEmpty(blockInfo) || (GetBlockID(blockInfo) == BlockManager.Instance.LavaBlockId);
    }

    public static Vector3I GlobalPositionToLocalBlockPosition(Godot.Vector3 globalPosition) {
		var blockpos = new Vector3I(Mathf.FloorToInt(globalPosition.X),
				Mathf.FloorToInt(globalPosition.Y),
				Mathf.FloorToInt(globalPosition.Z));
        var chunkpos = GlobalPositionToChunkPosition(globalPosition);
        blockpos -= chunkpos*CHUNK_SIZE;
        return blockpos + Vector3I.One;
    }

    public static Vector3I GlobalPositionToChunkPosition(Godot.Vector3 globalPosition) {
        return new Vector3I(
            Mathf.FloorToInt(globalPosition.X/CHUNK_SIZE),
            Mathf.FloorToInt(globalPosition.Y/CHUNK_SIZE*SUBCHUNKS),
            Mathf.FloorToInt(globalPosition.Z/CHUNK_SIZE)
        );
    }

    public static int BlockIndex(Vector3I blockPaddedPosition) {
        return blockPaddedPosition.X + blockPaddedPosition.Z*CSP + blockPaddedPosition.Y*CSP2*SUBCHUNKS;
    }


	public static Vector3I BlockIndexToVector(int blockIndex) {
		return new Vector3I(blockIndex%CSP,blockIndex/(CSP2*SUBCHUNKS),blockIndex/CSP%CSP);
	}

	public static int TryGetBlockInfoFromGlobalBlockPosition(Vector3I globalBlockPosition)
	{
		var chunk_position = GlobalPositionToChunkPosition(globalBlockPosition);
		if (Instance.BLOCKCACHE.TryGetValue(chunk_position, out var _blocks)) {
			var blockpos = GlobalPositionToLocalBlockPosition(globalBlockPosition);
			var _blockidx = BlockIndex(blockpos);
			return _blocks[_blockidx];
		} else GD.Print($"Tried to get block info from global block position {globalBlockPosition} but block data was not present in dictionary. Returning 0.");
		return 0;
	}

    #endregion
}