using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

public enum SlopeType {
    None=0,
    Side=1,
    Corner=2,
    InvCorner=3
}

[Tool]
public partial class ChunkTextureTest : Node3D
{
    const float INVSQRT2 = 0.70710678118f;

    public Dictionary<Vector3I, int[]> ChunkBlocksBuffer = new();

    private static readonly Godot.Vector3 SlopedNormalNegZ = new(0, INVSQRT2, -INVSQRT2);
    private static readonly Godot.Vector3 SlopedCornerNormalNegZ = new(INVSQRT2, INVSQRT2, -INVSQRT2);

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

    [Export] public MeshInstance3D ChunkMesh {get; set;}

    [Export] public bool FlipSlope {
        get => _flipslope;
        set {
            _flipslope = value;
            SlopeRotationDegrees = _slope_rotate_degrees;
        }
    }
    private bool _flipslope = false;

    [Export] public float SlopeRotationDegrees {
        get => _slope_rotate_degrees;
        set {
            _slope_rotate_degrees = value;
            GenerateTest((Vector3I)ChunkMesh.GlobalPosition);
            var blocks = ChunkBlocksBuffer[(Vector3I)ChunkMesh.GlobalPosition];
            var chunkmeshdata = BuildChunkMeshTest(blocks, false, _slope_rotate_degrees, _flipslope);
            ChunkMesh.Mesh = chunkmeshdata.UnifySurfaces();
        }
    }
    private float _slope_rotate_degrees = 0.0f;

    private Vector3I _prevChunkPosition = new(int.MinValue,int.MinValue,int.MinValue);
    private float _prevPlayerDistance = 0f;

    public static readonly ArrayMesh GrassBladeFullRes = ResourceLoader.Load("res://shaders/grass/blade.res") as ArrayMesh;
    public static readonly ArrayMesh GrassBladeLowRes = ResourceLoader.Load("res://shaders/grass/blade_optimized.tres") as ArrayMesh;

    public static readonly PackedScene GrassParticlesScene = ResourceLoader.Load("res://shaders/grass/grass_particles.tscn") as PackedScene;

    public static readonly ShaderMaterial GrassBladeMaterial = ResourceLoader.Load("res://shaders/grass/multimesh_grass_shader.tres") as ShaderMaterial;

    //[Export] public Grass GrassMultiMesh {get; set;}

    [Export] public Godot.Vector3 PlayerPosition {get; set;}

    public override void _Process(double delta) {
        var chunkpos = (Vector3I)ChunkMesh.GlobalPosition;
        var camera_position = EditorInterface.Singleton.GetEditorViewport3D(0).GetCamera3D().GlobalPosition;

        if (_prevChunkPosition != chunkpos && ChunkMesh != null && ChunkMesh is MeshInstance3D mesh && BlockManager.Instance != null) {
            if (!ChunkBlocksBuffer.TryGetValue(chunkpos, out var blocks)) {
                GenerateTest(chunkpos);
            }
            blocks = ChunkBlocksBuffer[chunkpos];
            var chunkmeshdata = BuildChunkMeshTest(blocks, false, SlopeRotationDegrees, _flipslope);
            mesh.Mesh = chunkmeshdata.UnifySurfaces();

            /*
            if (chunkmeshdata.HasSurfaceOfType(Chunk.ChunkMeshData.GRASS_SURFACE)) {
                var grass_mesh = chunkmeshdata.GetSurface(Chunk.ChunkMeshData.GRASS_SURFACE);
                GrassMultiMesh.TerrainMesh = grass_mesh;
                GrassMultiMesh.MaterialOverride = GrassBladeMaterial;
                GrassMultiMesh.ChunkPosition = chunkpos;
                GrassMultiMesh.PlayerPosition = camera_position;
            } else GrassMultiMesh.TerrainMesh = null;
            GrassMultiMesh.Rebuild();*/

            _prevChunkPosition = chunkpos;
        }

        // grass mesh LOD
        /*
        var player_dist = camera_position.DistanceTo(chunkpos+Godot.Vector3.One*Chunk.VOXEL_SCALE*Chunk.CHUNK_SIZE/2);
        if (player_dist > Chunk.CHUNK_SIZE*Chunk.VOXEL_SCALE*1.5f
        && _prevPlayerDistance <= Chunk.CHUNK_SIZE*Chunk.VOXEL_SCALE*1.5f) {
            GrassMultiMesh.ChunkPosition = chunkpos;
            GrassMultiMesh.PlayerPosition = camera_position;
            GrassMultiMesh.Rebuild();
            _prevPlayerDistance = player_dist;
        }
        else if (player_dist <= Chunk.CHUNK_SIZE*Chunk.VOXEL_SCALE*1.5f
        && _prevPlayerDistance > Chunk.CHUNK_SIZE*Chunk.VOXEL_SCALE*1.5f) {
            GrassMultiMesh.ChunkPosition = chunkpos;
            GrassMultiMesh.PlayerPosition = camera_position;
            GrassMultiMesh.Rebuild();
            _prevPlayerDistance = player_dist;
        }*/
    }

public void GenerateTest(Vector3I chunkPosition)
	{
        if (CantorPairing.Contains(chunkPosition)) {
            GD.Print("cantor pairing says Chunk already generated at ", chunkPosition);
            return;
        } 
        else GD.Print("Generating chunk at ", chunkPosition);

        if (!ChunkBlocksBuffer.TryGetValue(chunkPosition, out var result)) {
            result = new int[Chunk.CHUNKSQ*Chunk.CHUNK_SIZE*Chunk.SUBCHUNKS];
            ChunkBlocksBuffer[chunkPosition] = result;
        };

        var rnd = new RandomNumberGenerator();

        // blocks spawn when 3d noise is >= cutoff (its values are -1 to 1)
        var cutoff = 0.2f;

        List<Vector3I> filledBlocks = new();
        
        for (int subchunk = 0; subchunk < Chunk.SUBCHUNKS; subchunk++) {
            for (int x=0;x<Chunk.CHUNK_SIZE;x++) {
                for (int y=0;y<Chunk.CHUNK_SIZE;y++) {
                    for (int z=0;z<Chunk.CHUNK_SIZE;z++) {
                        int block_idx = x + y * Chunk.CHUNKSQ + z * Chunk.CHUNK_SIZE + subchunk*Chunk.CHUNKSQ*Chunk.CHUNK_SIZE;
                        if (block_idx >= result.Length) continue;

                        var globalBlockPosition = chunkPosition * new Vector3I(Chunk.Dimensions.X, Chunk.Dimensions.Y*Chunk.SUBCHUNKS, Chunk.Dimensions.Z)
                            + new Vector3I(x, y + Chunk.Dimensions.Y*subchunk, z);

                        int blockType = result[block_idx];
                        
                        // generate highest level differently
                        if (chunkPosition.Y == 3)
                        {
                            var noise3d = NOISE.GetNoise3D(globalBlockPosition.X, globalBlockPosition.Y, globalBlockPosition.Z);
                            if (y == 0 && noise3d >= cutoff) 
                            {
                                blockType = BlockManager.BlockID("Check2");
                            }
                            else if (y < 3 && block_idx-Chunk.CHUNKSQ > 0 && !Chunk.IsBlockEmpty(result[block_idx-Chunk.CHUNKSQ]))
                            {
                                blockType = BlockManager.BlockID("Check1");
                            }
                            else if (y == 3 && block_idx-Chunk.CHUNKSQ > 0 && !Chunk.IsBlockEmpty(result[block_idx-Chunk.CHUNKSQ]) && rnd.Randf() > 0.99)
                            {
                                // spawn tree or totem
                                result[block_idx-Chunk.CHUNKSQ] = BlockManager.InitBlockInfo(BlockManager.BlockID("Dirt"));
                                var blockSet = rnd.Randf() > 0.5 ? GenStructure.GenerateTotem(rnd.RandiRange(3,15)) : GenStructure.GenerateTree();

                                // blockset is a dictionary of block positions and block info ints (with all bits initialized)
                                foreach (KeyValuePair<Vector3I, int> kvp in blockSet)
                                {
                                    Vector3I p = new(x + kvp.Key.X, y + kvp.Key.Y, z + kvp.Key.Z);

                                    if (p.X < Chunk.CHUNK_SIZE && p.X >= 0
                                    &&  p.Y < Chunk.CHUNK_SIZE && p.Y >= 0
                                    &&  p.Z < Chunk.CHUNK_SIZE && p.Z >= 0)
                                    {
                                        if (Chunk.IsBlockEmpty(kvp.Value)) continue;
                                        result[p.X + p.Y*Chunk.CHUNKSQ + p.Z*Chunk.CHUNK_SIZE] = kvp.Value;
                                        filledBlocks.Add(new Vector3I(p.X, p.Y, p.Z));

                                        // randomly damage some blocks, but not leaves
                                        if (Chunk.GetBlockSpecies(kvp.Value) == BlockSpecies.Leaves) continue;
                                        var dam_amount = 0;
                                        var dam_type = 0;
                                        if (rnd.Randf() < 0.5)
                                        {
                                            dam_type = rnd.RandiRange(1, 7);
                                            dam_amount = rnd.RandiRange(0, 31);
                                        }
                                        var dam_info = (dam_type<<5) | dam_amount;

                                        // add the damage type 
                                        result[p.X + p.Y*Chunk.CHUNKSQ + p.Z*Chunk.CHUNK_SIZE] |= dam_info;
                                    } else {
                                        var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
                                        var dx = p.X > Chunk.CHUNK_SIZE-1 ? 1 : p.X < 0 ? -1 : 0;
                                        var dy = p.Y > Chunk.CHUNK_SIZE-1 ? 1 : p.Y < 0 ? -1 : 0;
                                        var dz = p.Z > Chunk.CHUNK_SIZE-1 ? 1 : p.Z < 0 ? -1 : 0;
                                        var delta = new Vector3I(dx, dy, dz);
                                        var newp = p-delta*Chunk.CHUNK_SIZE;
                                        if (!ChunkBlocksBuffer.TryGetValue(neighbour_chunk+delta, out var neighbour)) {
                                            neighbour = new int[Chunk.CHUNKSQ*Chunk.CHUNK_SIZE*Chunk.SUBCHUNKS];
                                            neighbour[newp.X + newp.Y*Chunk.CHUNKSQ + newp.Z*Chunk.CHUNK_SIZE] = kvp.Value;
                                        } else {
                                            var idx = newp.X + newp.Y*Chunk.CHUNKSQ + newp.Z*Chunk.CHUNK_SIZE;
                                            if (Chunk.IsBlockEmpty(neighbour[idx])) neighbour[idx] = kvp.Value;
                                        }
                                        ChunkBlocksBuffer[neighbour_chunk+delta] = neighbour;
                                    }
                                }
                                continue;
                            }
                            if (y>=3) continue; // skip here so we dont overwrite the tree with new blocks

                            // apply damage to upper layer blocks
                            int _damamount = 0, _damtype = 0;
                            if (rnd.Randf() < 0.5)
                            {
                                _damtype = rnd.RandiRange(1, 7);
                                _damamount = rnd.RandiRange(0, 31);
                            }
                            var _daminfo = (_damtype<<5) | _damamount;

                            result[block_idx] = Chunk.PackBlockInfo(blockType) | _daminfo;

                            if (blockType != 0) filledBlocks.Add(new Vector3I(x, y, z));

                            continue;
                        }

                        // generate other levels
                        var noise = CELLNOISE.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Z);
                        var groundheight = (int)(10*(noise+1)/2);
                        if (chunkPosition.Y == 0 && globalBlockPosition.Y <= groundheight)
                        {
                            if (y==0) blockType = BlockManager.Instance.LavaBlockId;
                            else if (globalBlockPosition.Y == groundheight) blockType = rnd.Randf() > 0.99 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Grass");
                            else if (globalBlockPosition.Y > groundheight - 3) blockType = BlockManager.BlockID("Dirt");
                            else blockType = rnd.Randf() > 0.9 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Stone");
                        }
                        else
                        {
                            var noise3d = NOISE.GetNoise3D(globalBlockPosition.X, globalBlockPosition.Y, globalBlockPosition.Z);
                            var noiseabove = NOISE.GetNoise3D(globalBlockPosition.X, globalBlockPosition.Y+1, globalBlockPosition.Z);
                            var noisebelow = NOISE.GetNoise3D(globalBlockPosition.X, globalBlockPosition.Y-1, globalBlockPosition.Z);
                            if (noise3d >= cutoff)
                            {
                                if (noiseabove < cutoff) {
                                    blockType = rnd.Randf() > 0.99 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Grass");
                                }
                                else if (noisebelow > noise3d) blockType = BlockManager.BlockID("Stone");
                                else blockType = BlockManager.BlockID("Dirt");
                            }
                        }

                        // add the damage type 
                        int blockinfo = 0;
                        blockinfo |= Chunk.PackBlockInfo(blockType);
                        result[block_idx] = blockinfo;

                        if (blockType != 0) filledBlocks.Add(new Vector3I(x, y, z));
                        
                        // add slope
                        /*
                        if (y>2) {
                            var altblockinfo = result[block_idx-2*Chunk.CHUNKSQ];
                            if (!Chunk.IsBlockEmpty(altblockinfo)) {
                                altblockinfo &= ~(0xff<<24);
                                ChunkBlocksBuffer[chunkPosition] = result;
                                var neighbour_bits = GetBlockNeighborBits(chunkPosition, new(x, y-2, z));
                                altblockinfo |= PackSlopeInfo(neighbour_bits);
                                result[block_idx-2*Chunk.CHUNKSQ] = altblockinfo;
                            }
                        }*/
                    }
                }
            }
        }

        // loop over all blocks again, add slopes
        Dictionary<Vector3I,int> packedSlopeBlockData = new();

        // add corner and side slopes
        foreach (var p in filledBlocks) {
            var i = p.X + p.Y*Chunk.CHUNKSQ + p.Z*Chunk.CHUNK_SIZE;
            if (!Chunk.IsBlockEmpty(result[i]) && BlockIsSlopeCandidate(chunkPosition, p)) {
                var s = BlockPackSlopeInfo(chunkPosition, p);
                if (Chunk.GetBlockSlopeType(s) == (int)SlopeType.Corner) {
                    var newidx = p.X + (p.Y-1)*Chunk.CHUNKSQ + p.Z*Chunk.CHUNK_SIZE;
                    if (newidx >= 0) result[newidx] = result[i];
                }
                packedSlopeBlockData[p] = s;
            }
        }

        foreach (var (p, packedSlopeData) in packedSlopeBlockData) {
            var i = p.X + p.Y*Chunk.CHUNKSQ + p.Z*Chunk.CHUNK_SIZE;
            result[i] |= packedSlopeData;
        }

        ChunkBlocksBuffer[chunkPosition] = result;
        CantorPairing.Add(chunkPosition);
	}

    public int BlockPackSlopeInfo(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        Vector3I chunkpos = new(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);

        var slopetype = 0;
        var sloperot = 0;

        // these functions assume that the block is a slope candidate already (not empty, surrounded, has a block below and no block above)


        if (BlockIsCorner(chunkpos, p)) {
            slopetype = 2;
            sloperot = BlockCornerRotation(chunkpos, p);
        }
        if (BlockIsSide(chunkpos, p)) {
            slopetype = 1;
            sloperot = BlockSideRotation(chunkpos, p);
        }

        if (BlockIsInvCorner(chunkpos, p)) {
            slopetype = 3;
            sloperot = BlockInvCornerRotation(chunkpos, p);
        }

        return Chunk.PackSlopeData(slopetype, sloperot);
    }

    public bool BlockIsSlopeCandidate(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return IsBlockBelow(chunkPosition, p) && !IsBlockAbove(chunkPosition, p) && (!BlockIsSurrounded(chunkPosition, p)||BlockSingleDiagonalFree(chunkPosition, p));
    }

    public bool BlockIsInvCorner(Vector3I chunkPosition, Vector3I blockPosition) {
        return BlockIsSurrounded(chunkPosition, blockPosition)&&BlockSingleDiagonalFree(chunkPosition, blockPosition);
    }

    public int BlockInvCornerRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        var b1 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Right);
        var b2 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Left);
        var b3 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Right);
        var b4 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Left);

        if (b1) return 0;
        if (b2) return 1;
        if (b4) return 2;
        if (b3) return 3;
        return 0;
    }

    public bool BlockIsCorner(Vector3I chunkPosition, Vector3I blockPosition) {
        // these functions assume that the block is a slope candidate already (not empty, surrounded, has a block below and no block above)
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);


        if (
                ! (
                    (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsBlockAbove(chunkPosition, p+Vector3I.Forward) && !IsBlockAbove(chunkPosition, p+Vector3I.Right))
                    ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsBlockAbove(chunkPosition, p+Vector3I.Forward) && !IsBlockAbove(chunkPosition, p+Vector3I.Left))
                    ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsBlockAbove(chunkPosition, p+Vector3I.Back) && !IsBlockAbove(chunkPosition, p+Vector3I.Right))
                    ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsBlockAbove(chunkPosition, p+Vector3I.Back) && !IsBlockAbove(chunkPosition, p+ Vector3I.Left))
                )
            ) return false;


        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Forward);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Right);
            if (!Chunk.IsBlockSloped(b1) && !Chunk.IsBlockSloped(b2)) return true;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Forward);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Left);
            if (!Chunk.IsBlockSloped(b1) && !Chunk.IsBlockSloped(b2)) return true;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Back);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Right);
            if (!Chunk.IsBlockSloped(b1) && !Chunk.IsBlockSloped(b2)) return true;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Back);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Left);
            if (!Chunk.IsBlockSloped(b1) && !Chunk.IsBlockSloped(b2)) return true;
        }

        return false;
    }

    public int BlockCornerRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) return 2;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) return 3;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) return 0;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) return 1;
        return 0;
    }

    public bool IsBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        return BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Up);
    }

    public bool IsBlockBelow(Vector3I chunkPosition, Vector3I blockPosition) {
        return BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Down);
    }

    public bool BlockIsSurrounded(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return BlockHasNeighbor(chunkPosition, p, Vector3I.Forward)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Back)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Right);
    }

    public bool BlockSingleDiagonalFree(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        var b1 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Right);
        var b2 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Left);
        var b3 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Right);
        var b4 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Left);

        return (!b1 && b2 && b3 && b4) ^ (b1 && !b2 && b3 && b4) ^ (b1 && b2 && !b3 && b4) ^ (b1 && b2 && b3 && !b4);
    }

    public int BlockDiagonalsCount(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        int count = 0;
        for (int x=-1;x<2;x+=2) {
                for (int z=-1;z<2;z+=2) {
                    if (BlockHasNeighbor(chunkPosition, p, new Vector3I(p.X+x, p.Y, p.Z+z))) count++;
                }
        }
        return count;
    }

    public int BlockNeighbourCount(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        int count = 0;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) count++;
        return count;
    }

    public bool BlockIsSide(Vector3I chunkPosition, Vector3I blockPosition) {
        // these functions assume that the block is a slope candidate already (not empty, surrounded, has a block below and no block above)
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (!(BlockNeighbourCount(chunkPosition, p) == 3 || BlockNeighbourCount(chunkPosition, p) == 1)) return false;

        if (BlockNeighbourCount(chunkPosition, p) == 1) {
            return
                (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsBlockAbove(chunkPosition, p+Vector3I.Forward))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsBlockAbove(chunkPosition, p+Vector3I.Back))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsBlockAbove(chunkPosition, p+Vector3I.Left))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsBlockAbove(chunkPosition, p+Vector3I.Right));
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Back))
        {
            return
                (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsBlockAbove(chunkPosition, p+Vector3I.Left))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsBlockAbove(chunkPosition, p+Vector3I.Right));

        }
        else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left))
        {
            return
                (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsBlockAbove(chunkPosition, p+Vector3I.Forward))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsBlockAbove(chunkPosition, p+Vector3I.Back));
        }

        return false;
    }

    public int BlockSideRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is slope
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (BlockNeighbourCount(chunkPosition, p) == 1) {
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsBlockAbove(chunkPosition, p+Vector3I.Forward)) return 2;
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsBlockAbove(chunkPosition, p+Vector3I.Back)) return 0;
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsBlockAbove(chunkPosition, p+Vector3I.Left)) return 3;
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsBlockAbove(chunkPosition, p+Vector3I.Right)) return 1;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Back))
        {
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsBlockAbove(chunkPosition, p+Vector3I.Right)) return 1;
            else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsBlockAbove(chunkPosition, p+Vector3I.Left)) return 3;  
        }
        else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left))
        {
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsBlockAbove(chunkPosition, p+Vector3I.Forward)) return 2;
            else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsBlockAbove(chunkPosition, p+Vector3I.Back)) return 0;
        }
    

        return 0;
    }

    public bool BlockHasNeighbor(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection) {
        return GetBlockNeighbour(chunkPosition, blockPosition, neighborDirection) != 0;
    }

    public int GetBlockNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection) {
        var chunk = ChunkBlocksBuffer[chunkPosition];
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);

        if (p.X < Chunk.CHUNK_SIZE && p.X >= 0
        &&  p.Y < Chunk.CHUNK_SIZE && p.Y >= 0
        &&  p.Z < Chunk.CHUNK_SIZE && p.Z >= 0)
        {
            var b = chunk[p.X + p.Y*Chunk.CHUNKSQ + p.Z*Chunk.CHUNK_SIZE];
            if (!Chunk.IsBlockEmpty(b)) return b;
        }
        else
        {
            var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
            var dx = p.X > Chunk.CHUNK_SIZE-1 ? 1 : p.X < 0 ? -1 : 0;
            var dy = p.Y > Chunk.CHUNK_SIZE-1 ? 1 : p.Y < 0 ? -1 : 0;
            var dz = p.Z > Chunk.CHUNK_SIZE-1 ? 1 : p.Z < 0 ? -1 : 0;
            var delta = new Vector3I(dx, dy, dz);
            var newp = p-delta*Chunk.CHUNK_SIZE;
            if (!ChunkBlocksBuffer.TryGetValue(neighbour_chunk+delta, out var neighbour)) {
                neighbour = new int[Chunk.CHUNKSQ*Chunk.CHUNK_SIZE*Chunk.SUBCHUNKS];
                ChunkBlocksBuffer[neighbour_chunk+delta] = neighbour;
            }

            var new_idx = newp.X + newp.Y*Chunk.CHUNKSQ + newp.Z*Chunk.CHUNK_SIZE;
            if (!Chunk.IsBlockEmpty(neighbour[new_idx])) return neighbour[new_idx];
        }

        return 0;
    }
    public static Chunk.ChunkMeshData BuildChunkMeshTest(int[] chunk_blocks, bool isLowestChunk, float sloperotation, bool flipSlope = false) {
        // data is an array of dictionaries, one for each axis
        // each dictionary is a hash map of block types to a set binary planes
        // we need to group by block type like this so we can batch the meshing and texture blocks correctly
        Dictionary<int, Dictionary<int, UInt32[]>>[] data = new Dictionary<int,Dictionary<int, UInt32[]>>[7];
        short i;
        for (i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value
        data[i] = new(); // an extra one for sloped blocks

        // add all Chunk.SUBCHUNKS
        for (i=0; i < Chunk.SUBCHUNKS; i++) GreedyChunkMeshTest(data, chunk_blocks, i);

        // construct mesh
        var _st = new SurfaceTool();
        var _st2 = new SurfaceTool();
        var grassTopSurfaceTool = new SurfaceTool();
        _st.Begin(Mesh.PrimitiveType.Triangles);
        _st2.Begin(Mesh.PrimitiveType.Triangles);
        grassTopSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        for (int axis=0; axis<6;axis++) {
            foreach (var (blockinfo, planeSet) in data[axis]) {
                foreach (var (k_chunked, binary_plane) in planeSet) {
                    var blockId = Chunk.GetBlockID(blockinfo);

                    // sloped blocks are not greedy meshed
                    var greedy_quads = GreedyMeshBinaryPlane(binary_plane);

                    var k = k_chunked % Chunk.Dimensions.Y;
                    var subchunk = k_chunked/Chunk.Dimensions.Y;

                    foreach (GreedyQuad quad in greedy_quads) {
                        Vector3I quad_offset, quad_delta; // row and col, width and height
                        Godot.Vector2 uv_offset;

                        quad_offset = axis switch
                        {
                            // row, col -> axis
                            0 => new Vector3I(quad.col, k, quad.row), // down, up    (xz -> y axis)
                            1 => new Vector3I(quad.col, k+1, quad.row), 
                            2 => new Vector3I(k, quad.row, quad.col), // left, right (zy -> x axis)
                            3 => new Vector3I(k+1, quad.row, quad.col), 
                            4 => new Vector3I(quad.col, quad.row, k), // back, front (xy -> z axis)
                            _ => new Vector3I(quad.col, quad.row, k+1)  // remember -z is forward in godot, we are still in chunk space so we add 1
                        };

                        quad_delta = axis switch
                        {
                            // row, col -> axis
                            0 or 1 => new Vector3I(quad.delta_col, 0, quad.delta_row),  // down, up    (xz -> y axis)
                            2 or 3 => new Vector3I(0, quad.delta_row, quad.delta_col),  // right, left (zy -> x axis)
                            _ => new Vector3I(quad.delta_col, quad.delta_row, 0),       // back, front (xy -> z axis)
                        };

                        uv_offset = axis switch
                        {
                            0 => new Godot.Vector2(quad_delta.X, quad_delta.Z), // down, up    (xz -> y axis)
                            1 => new Godot.Vector2(quad_delta.Z, quad_delta.X), // for some reason y is flipped on the top face???? :( 
                            2 or 3 => new Godot.Vector2(quad_delta.Z, quad_delta.Y), // right, left (zy -> x axis)
                            _ => new Godot.Vector2(quad_delta.X, quad_delta.Y),      // back, front (xy -> z axis)
                        };

                        // offset vertical by the current subchunk
                        // note that subchunking isnt even implemented because it turned out slower than just multithreading everything
                        // so SUBCHUNKS should be fixed at 1 and this always adds 0
                        quad_offset += Vector3I.Up*subchunk*Chunk.Dimensions.Y;

                        // construct vertices and normals for mesh
                        Godot.Vector3[] verts = new Godot.Vector3[4];
                        for (i=0; i<4; i++) {
                            verts[i] = quad_offset + (Godot.Vector3)CUBE_VERTS1[AXIS1[axis,i]]*quad_delta;

                            // if the lowest block level, push the bottom verts down by 100
                            // this makes a bottomless pit instead of just cutting the map off in a void
                            // we add a death field below the map to catch the player
                            if (isLowestChunk && verts[i].Y == 0) {
                                verts[i] -= Godot.Vector3.Up*100f;
                            }
                        }

                        Godot.Vector3[] triangle1 = {verts[0], verts[1], verts[2]};
                        Godot.Vector3[] triangle2 = {verts[0], verts[2], verts[3]};
                        Godot.Vector3 normal = axis switch
                        {
                            0 => Godot.Vector3.Down, // -y
                            1 => Godot.Vector3.Up,   // +y
                            2 => Godot.Vector3.Left, // -x
                            3 => Godot.Vector3.Right, // +x
                            4 => Godot.Vector3.Forward, // -z is forward in godot
                            _ => Godot.Vector3.Back     // +z
                        };
                        Godot.Vector3[] normals = {normal, normal, normal};

                        
                        var uvA = Godot.Vector2.Zero;
                        var uvB = new Godot.Vector2(0, 1);
                        var uvC = Godot.Vector2.One;
                        var uvD = new Godot.Vector2(1, 0);
                        var uvTriangle1 = new Godot.Vector2[] { uvA, uvB, uvC };
		                var uvTriangle2 = new Godot.Vector2[] { uvA, uvC, uvD };

                        // add the quad to the mesh
                        if (blockId == BlockManager.Instance.LavaBlockId)
                        {
                            _st2.AddTriangleFan(triangle1, uvTriangle1, normals: normals);
                            _st2.AddTriangleFan(triangle2, uvTriangle2, normals: normals);
                        }
                        else
                        {
                            var blockDamage = Chunk.GetBlockDamageData(blockinfo);
                            var block_face_texture_idx = BlockManager.BlockTextureArrayPositions(blockId)[axis];
                            var notacolour = new Color(block_face_texture_idx, uv_offset.X, uv_offset.Y, blockDamage)*(1/255f);
                            var metadata = new Color[] {notacolour, notacolour, notacolour};

                            if (blockId == BlockManager.BlockID("Grass") && axis == 1)
                            {
                                grassTopSurfaceTool.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                                grassTopSurfaceTool.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                            }
                            else
                            {
                                _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                                _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                            }
                        }
                    }
                }
            }
        }

        // sloped blocks are not greedy meshed, but constucted seperately
        // their data is stored in the 7th dictionary
        foreach (var (chunk_idx, blockdata) in data[6][0])
        {
            var blockinfo = (int)blockdata[0];
            var blockId = Chunk.GetBlockID(blockinfo);
            var slopeType = Chunk.GetBlockSlopeType(blockinfo);

            // two types of slope, regular slope (id:1) or angled (7 face) corner slope (id:2)
            // all blocks in this set are sloped so it's either going to be 1 or 2
            var regularSlope = slopeType == (int)SlopeType.Side;
            var cornerSlope = slopeType == (int)SlopeType.Corner;
            var invCornerSlope = slopeType == (int)SlopeType.InvCorner;
            float rotation_angle = Chunk.GetBlockSlopeRotation(blockinfo);
            //rotation_angle += Mathf.Pi/2;
            while (rotation_angle > Mathf.Pi*2) rotation_angle -= Mathf.Pi*2;
            // DEBUG no flip slope
            //if (flipSlope && !regularSlope) rotation_degrees -= 90f;

            var x = chunk_idx % Chunk.CHUNK_SIZE;
            var z = (chunk_idx / Chunk.CHUNK_SIZE) % Chunk.CHUNK_SIZE;
            var y = chunk_idx / Chunk.CHUNKSQ;
            Vector3I pos = new(x,y,z); 

            for (int axis=0;axis<6;axis++)
            {
                // regular slope - skip front face because it's a ramp
                if (regularSlope && axis==4) continue;

                //pos += quad_offset;

                var blockDamage = Chunk.GetBlockDamageData(blockinfo);
                var block_face_texture_idx = BlockManager.BlockTextureArrayPositions(blockId)[axis];
                var notacolour = new Color(block_face_texture_idx, 1.0f, 1.0f, blockDamage)*(1/255f);
                var metadata = new Color[] {notacolour, notacolour, notacolour};

                Godot.Vector3[] verts = new Godot.Vector3[4];

                for (i=0; i<4; i++) {
                    // get local vertex coords
                    verts[i] = (Godot.Vector3) CUBE_VERTS1[AXIS1[axis,i]] - Godot.Vector3.One * 0.5f;

                    // shift down top face into a slope, for regular slope
                    if (regularSlope && axis==1 && (i==0 || i==1)) verts[i] -= Godot.Vector3.Up;
                    if (cornerSlope && axis==1 && (i==0 || i==1 || i ==2)) verts[i] -= Godot.Vector3.Up; // else shift corner down by 1 for corner slopes
                    if (invCornerSlope && axis==1 && i==1) verts[i] -= Godot.Vector3.Up; // else shift corner down by 1
                    
                    if (flipSlope) verts[i] = verts[i].Rotated(Godot.Vector3.Forward, Mathf.Pi);
                    verts[i] = verts[i].Rotated(Godot.Vector3.Up, rotation_angle);
                    verts[i] += (Godot.Vector3)pos+Godot.Vector3.One*0.5f;
                }
                
                Godot.Vector3[] triangle1 = {verts[0], verts[1], verts[2]};
                Godot.Vector3[] triangle2 = {verts[0], verts[2], verts[3]};
                Godot.Vector3 normal = axis switch
                {
                    0 => Godot.Vector3.Down,    // -y
                    1 => Godot.Vector3.Up,      // +y
                    2 => Godot.Vector3.Left,    // -x
                    3 => Godot.Vector3.Right,   // +x
                    4 => Godot.Vector3.Forward, // -z is forward in godot
                    _ => Godot.Vector3.Back     // +z
                };
                if (flipSlope) normal = normal.Rotated(Godot.Vector3.Forward, Mathf.Pi);
                normal = normal.Rotated(Godot.Vector3.Up, rotation_angle);

                Godot.Vector3[] normals = {normal, normal, normal};
                
                var uvA = Godot.Vector2.Zero;
                var uvB = new Godot.Vector2(0, 1);
                var uvC = Godot.Vector2.One;
                var uvD = new Godot.Vector2(1, 0);
                var uvTriangle1 = new Godot.Vector2[] { uvA, uvB, uvC };
                var uvTriangle2 = new Godot.Vector2[] { uvA, uvC, uvD };

                switch (axis)
                {
                    case 1: // top face - modify normals
                        if (invCornerSlope) _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        
                        var normrotate = SlopedNormalNegZ;
                        if (cornerSlope || invCornerSlope) normrotate = SlopedCornerNormalNegZ;
                        if (flipSlope) normrotate = normrotate.Rotated(Godot.Vector3.Forward, Mathf.Pi);
                        normrotate = normrotate.Rotated(Godot.Vector3.Up, rotation_angle);
                        normals = new Godot.Vector3[] {normrotate,normrotate,normrotate};
                        if (regularSlope || invCornerSlope) _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);

                        if (!invCornerSlope) _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        break;
                    case 2: // side face, only add one of the triangles
                        if (regularSlope || cornerSlope) _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        else if (invCornerSlope) {
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals); 
                        }
                        break;
                    case 3: // obverse side face, only add one of the triangles and adjust its vertices accordingly
                        triangle1 = new Godot.Vector3[] {verts[1], verts[2], verts[3]};

                        //if (invCornerSlope) uvTriangle1 = new Godot.Vector2[] { uvC, uvB, uvA };
                        if (regularSlope || invCornerSlope) {
                            uvTriangle1 = new Godot.Vector2[] { uvC, uvB, uvA };
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        }
                        break;
                    case 4: // facing -z, front, corner slopes only add one triangle, else normal
                        if (regularSlope) {
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                        else if (invCornerSlope) _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        break;
                    case 5:
                        if (regularSlope || invCornerSlope) {
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                        if (cornerSlope) {
                            triangle1 = new Godot.Vector3[] {verts[1], verts[2], verts[3]};
                            uvTriangle1 = new Godot.Vector2[] { uvC, uvB, uvA };
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        }
                        break;
                    default: // bottom face is always drawn
                        _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        break;
                }
            }
        }

        grassTopSurfaceTool.Index();
        var a1 = _st.Commit();
        var a2 = _st2.Commit();
        var a3 = grassTopSurfaceTool.Commit();
        
        var surfaces = new ArrayMesh[Chunk.ChunkMeshData.MAX_SURFACES];
        surfaces[Chunk.ChunkMeshData.CHUNK_SURFACE] = a1;
        surfaces[Chunk.ChunkMeshData.LAVA_SURFACE] = a2;
        surfaces[Chunk.ChunkMeshData.GRASS_SURFACE] = a3;

        return new Chunk.ChunkMeshData(surfaces);
    }

private static List<GreedyQuad> GreedyMeshBinaryPlane(UInt32[] data) {
        List<GreedyQuad> greedy_quads = new();
        int data_length = data.Length;
        for (int j=0;j<data_length;j++) { // j selects a row from the data[j]
            var i = 0; // i  traverses the bits in current row j
            while (i < Chunk.CHUNK_SIZE) {
                i += BitOperations.TrailingZeroCount(data[j] >> i);
                if (i>=Chunk.CHUNK_SIZE) continue;
                var h = BitOperations.TrailingZeroCount(~(data[j] >> i)); // count trailing ones from i upwards
                UInt32 h_as_mask = 0; // create a mask of h bits
                for (int xx=0;xx<h;xx++) h_as_mask |= (UInt32)1 << xx;
                var mask = h_as_mask << i; // a mask of h bits starting at i
                var w = 1;
                while (j+w < data_length) {
                    var next_row_h = (data[j+w] >> i) & h_as_mask; // check next row across
                    if (next_row_h != h_as_mask) break; // if we can't expand aross the row, break
                    data[j+w] &= ~mask;  // if we can, we clear bits from next row so they won't be processed again
                    w++;
                }
                greedy_quads.Add(new GreedyQuad{row=j, col=i, delta_row=w, delta_col=h}); 
                i+=h; // jump past the ones to check if there are any more in this column
            }
        }
        return greedy_quads;
    }

    public struct GreedyQuad {
        public int col; // column offset
        public int row; // row offset
        public int delta_row; // width of quad
        public int delta_col; // height of quad

        public GreedyQuad(int col, int row, int w, int h) {
            this.col = col;
            this.row = row;
            this.delta_row = w;
            this.delta_col = h;
        }
    }

    private static readonly Vector3I[] CUBE_VERTS1 = 
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
    private static readonly int[,] AXIS1 = 
        {
            {0, 4, 5, 1}, // bottom
            {2, 3, 7, 6}, // top
            {6, 4, 0, 2}, // left
            {3, 1, 5, 7}, // right
            {2, 0, 1, 3}, // front
            {7, 5, 4, 6}  // back
        };

public static void GreedyChunkMeshTest(Dictionary<int, Dictionary<int, UInt32[]>>[] data, int[] chunk_blocks, int subchunk) {
        var axis_cols = new UInt32[Chunk.CSP3*3];
        var col_face_masks = new UInt32[Chunk.CSP3*6];

        var slope_blocks = new Dictionary<int, UInt32[]>();

        // generate binary 0 1 voxel representation for each axis
        for (int x=0;x<Chunk.CSP;x++) {
            for (int y=0;y<Chunk.CSP;y++) {
                for (int z=0;z<Chunk.CSP;z++) {
                    var pos = new Vector3I(x,y,z)-Vector3I.One; // goofy ahh check for out of bounds
                    if (pos.X<0||pos.X>=Chunk.CHUNK_SIZE||pos.Y<0||pos.Y>=Chunk.CHUNK_SIZE||pos.Z<0||pos.Z>=Chunk.CHUNK_SIZE) continue; 
                    var chunk_idx = pos.X + pos.Z*Chunk.CHUNK_SIZE + pos.Y*Chunk.CHUNKSQ;
                    chunk_idx += subchunk*Chunk.CHUNKSQ*Chunk.CHUNK_SIZE; // move up one subchunk
                    
                    var b = chunk_blocks[chunk_idx];
                    if (Chunk.IsBlockSloped(b)) {
                        // add sloped blocks and IDs to a separate list
                        if (!slope_blocks.TryGetValue(chunk_idx, out _ )) {
                            slope_blocks.Add(chunk_idx, new UInt32[] {(uint)b});
                        }
                    }
                    else if (!Chunk.IsBlockEmpty(b)) { // if block is solid
                        axis_cols[x + z*Chunk.CSP] |= (UInt32)1 << y;           // y axis defined by x,z
                        axis_cols[z + y*Chunk.CSP + Chunk.CSP2] |= (UInt32)1 << x;    // x axis defined by z,y
                        axis_cols[x + y*Chunk.CSP + Chunk.CSP2*2] |= (UInt32)1 << z;  // z axis defined by x,y
                    }
                }
            }
        }

        // add slope blocks to entry zero of the extra "axis"
        // data 1-5 are the cube axes, 6 is the sloped blocks 
        data[6].Add(0, slope_blocks);

        // do face culling for each axis
        for (int axis = 0; axis < 3; axis++) {
            for (int i=0; i<Chunk.CSP2; i++) {
                var col = axis_cols[i + axis*Chunk.CSP2];
                // sample descending axis and set true when air meets solid
                col_face_masks[Chunk.CSP2*axis*2 + i] = col & ~(col << 1);
                // sample ascending axis and set true when air meets solid
                col_face_masks[Chunk.CSP2*(axis*2+1) + i] = col & ~(col >> 1);
            }
        }

        // put the data into the hash maps
        for (int axis = 0; axis < 6; axis++) {
            // i and j are coords in the binary plane for the given axis
            // i is column, j is row
            for (int j=0;j<Chunk.CHUNK_SIZE;j++) {
                for (int i=0;i<Chunk.CHUNK_SIZE;i++) {
                    // get column index for col_face_masks
                    // add 1 to i and j because we are skipping the first row and column due to padding
                    var col_idx = (i+1) + ((j+1) * Chunk.CSP) + (axis * Chunk.CSP2);

                    // removes rightmost and leftmost padded bit (it's outside the chunk)
                    var col = col_face_masks[col_idx] >> 1;
                    col &= ~((UInt32)1 << Chunk.CHUNK_SIZE);

                    // now get y coord of faces (it's their bit location in the UInt64, so trailing zeroes can find it)
                    while (col != 0) {
                        var k = BitOperations.TrailingZeroCount(col);
                        // clear least significant (rightmost) set bit
                        col &= col-1;

                        var voxel_pos = axis switch
                            {
                                0 or 1 => new Vector3I(i, k, j),  // down, up    (xz -> y axis)
                                2 or 3 => new Vector3I(k, j, i),  // right, left (zy -> x axis)
                                _ => new Vector3I(i, j, k),       // back, front (xy -> z axis)
                            };
                        var blockinfo = chunk_blocks[
                                voxel_pos.X
                                + voxel_pos.Z * Chunk.CHUNK_SIZE
                                + voxel_pos.Y * Chunk.CHUNKSQ
                                + subchunk*Chunk.CHUNKSQ*Chunk.CHUNK_SIZE
                            ];

                        
                        if (!data[axis].TryGetValue(blockinfo, out Dictionary<int, UInt32[]> planeSet)) {
                            planeSet = new(); 
                            data[axis].Add(blockinfo, planeSet);
                        }

                        var k_ymod = k+Chunk.Dimensions.Y*subchunk;
                        if (!planeSet.TryGetValue(k_ymod, out UInt32[] data_entry)) {
                            data_entry = new UInt32[Chunk.CHUNK_SIZE];
                            planeSet.Add(k_ymod, data_entry);
                        }
                        data_entry[j] |= (UInt32)1 << i;     // push the "row" bit into the "column" UInt32
                        planeSet[k_ymod] = data_entry;
                    }
                }
            }
        }
    }
}