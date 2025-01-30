using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public partial class ChunkManager : Node {

	public const int CHUNK_SIZE = 30; // the max chunk size is 30, padded chunk size is 32, // must match size in compute shader
    public const int CHUNKSQ = CHUNK_SIZE*CHUNK_SIZE;
    public const int CSP = CHUNK_SIZE+2;
    public const int CSP2 = CSP*CSP; // squared padded chunk size
    public const int CSP3 = CSP2*CSP; // cubed padded chunk size
    // chunk size is 30, padded chunk size is 32. Can't be increased easily because it uses binary UINT32 to do face culling
    public const int SUBCHUNKS = 1; // each one is an extra 32x32 chunk in the vertical y direction
	public static readonly Vector3I Dimensions = new(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);

    // in the Generate() method, noise >= this value doesnt generate blocks
    public const float NOISE_CUTOFF = 0.2f;

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

    // blocks spawn when 3d noise is >= _cutoff (its values are -1 to 1)
    private float _cutoff = 0.2f;
    private float _scalefactor = 1.0f;

#region generate chunk
    public static void Generate(Vector3I chunkPosition)
	{
        if (CantorPairing.Contains(chunkPosition)) {
            GD.Print("cantor pairing says Chunk already generated at ", chunkPosition);
            return;
        }
        else {
            //GD.Print("Generating chunk at ", chunkPosition);
            CantorPairing.Add(chunkPosition);
        }

        if (!Instance.BLOCKCACHE.TryGetValue(chunkPosition, out var result)) {
            result = new int[CSP3*SUBCHUNKS];
        }

        var rnd = new RandomNumberGenerator();

        List<Vector3I> filledBlocks = new();
        
        for (int subchunk = 0; subchunk < SUBCHUNKS; subchunk++) {
            for (int x=0;x<CSP;x++) {
                for (int y=0;y<CSP;y++) {
                    for (int z=0;z<CSP;z++) {
                        int block_idx = x + y * CSP2 + z * CSP + subchunk*CSP3;
                        if (block_idx >= result.Length) continue;

                        // keep cached state up to date as you go
                        // if you don't do this, you can fall out of synch
                        // and get structures cut off between chunks
                        Instance.BLOCKCACHE[chunkPosition] = result;
                        int blockType = result[block_idx];

                        var globalBlockPosition = chunkPosition * new Vector3I(Dimensions.X, Dimensions.Y*SUBCHUNKS, Dimensions.Z)
                            + new Vector3I(x, y + CSP2*subchunk, z) - Vector3I.One;

                        /*
                        if (!Instance.BLOCKCACHE.TryGetValue(chunkPosition, out var settype))
                            settype = result;
                        int blockType = settype[block_idx];*/
                        if (!IsBlockEmpty(blockType)) {
                            // ignore blocks already filled by neighbours
                            filledBlocks.Add(new Vector3I(x, y, z));
                            continue;
                        }
                        else blockType = GetBlockID(blockType);

                        // check for out of bounds first
                        if (chunkPosition.Y == 0 && y == 1)
                        {
                            result[block_idx] = PackAllBlockInfo(BlockManager.Instance.LavaBlockId,0,0,0,0,0);
                            continue;
                        }

                        // pad y levels with air
                        // this stops slopes which spawn at y==CHUNKS_SIZE from being wrongly oriented because of erroneous padding
                        if (y == CSP-1)
                        {
                            result[block_idx] = PackAllBlockInfo(0,0,0,0,0,0);
                            continue;
                        }

                        // generate highest level differently
                        if (chunkPosition.Y == 3)
                        {
                            var noise3d = NOISE.GetNoise3D(Instance._scalefactor*globalBlockPosition.X, Instance._scalefactor*globalBlockPosition.Y, Instance._scalefactor*globalBlockPosition.Z);
                            if (y == 0 && noise3d >= Instance._cutoff) 
                            {
                                blockType = BlockManager.BlockID("Check2");
                            }
                            else if (y < 3 && block_idx-CSP2 > 0 && !IsBlockEmpty(result[block_idx-CSP2]))
                            {
                                blockType = BlockManager.BlockID("Check1");
                            }
                            else if (y == 3 && block_idx-CSP2 > 0 && !IsBlockEmpty(result[block_idx-CSP2]) && rnd.Randf() > 0.99)
                            {
                                // spawn tree or totem
                                result[block_idx-CSP2] = PackBlockType(BlockManager.BlockID("Dirt"));
                                var blockSet = rnd.Randf() > 0.5 ? GenStructure.GenerateTotem(rnd.RandiRange(3,15)) : GenStructure.GenerateTree();

                                // blockset is a dictionary of block positions and block info ints (with all bits initialized)
                                foreach ((var v, var genblockinfo) in blockSet)
                                {
                                    Vector3I p = new(x + v.X, y + v.Y, z + v.Z);
                                    if (p.X >= 0 && p.X < CSP && p.Y >= 0 && p.Y < CSP && p.Z >= 0 && p.Z < CSP)
                                    {
                                        var newidx = BlockIndex(p);
                                        result[newidx] = PackAllBlockInfo(genblockinfo,0,0,0,0,0);
                                        if (!IsBlockEmpty(result[newidx])) filledBlocks.Add(p);
                                    }
                                    Instance.BLOCKCACHE[chunkPosition] = result;
                                    SetBlockChunkNeighbour(chunkPosition,p,Vector3I.Zero, genblockinfo);
                                    result = Instance.BLOCKCACHE[chunkPosition];
                                }
                                continue;
                            }
                            result[block_idx] = PackAllBlockInfo(blockType,0,0,0,0,0);
                            if (blockType != 0) filledBlocks.Add(new Vector3I(x, y, z));

                            if (y>3) continue; // skip here so we dont overwrite the tree with new blocks

                            // apply damage to upper layer blocks
                            int _damamount = 0, _damtype = 0;
                            if (rnd.Randf() < 0.5)
                            {
                                _damtype = rnd.RandiRange(1, 7);
                                _damamount = rnd.RandiRange(0, 31);
                            }
                            
                            result[block_idx] = PackAllBlockInfo(blockType,_damtype,_damamount,0,0,0);
                            if (!IsBlockEmpty(result[block_idx])) filledBlocks.Add(new Vector3I(x, y, z));

                            continue;
                        }
                        else if (chunkPosition.Y == 2) {
                            // set block type to check2 if noise is above cutoff for global chunk y==2
                            var noise3d = NOISE.GetNoise3D(globalBlockPosition.X*Instance._scalefactor, globalBlockPosition.Y*Instance._scalefactor, globalBlockPosition.Z*Instance._scalefactor);
                            if (noise3d >= Instance._cutoff) 
                            {
                                blockType = BlockManager.BlockID("Check2");
                            }
                        }

                        // generate other levels - set blockType
                        var noise = NOISE.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Z);
                        var groundheight = (int)(10*(noise+1)/2);
                        if (globalBlockPosition.Y<=groundheight)
                        {
                            if (globalBlockPosition.Y < groundheight && globalBlockPosition.Y > groundheight - 3) blockType = BlockManager.BlockID("Stone");
                            else if (globalBlockPosition.Y == groundheight) blockType = rnd.Randf() > 0.99 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Grass");
                            else if (globalBlockPosition.Y > groundheight) blockType = BlockManager.BlockID("Air");
                            else blockType = rnd.Randf() > 0.9 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Stone");
                        }
                        else
                        {
                            //var height = TerraceFunc(globalBlockPosition.Y*Instance._scalefactor);
                            var noise3d = NOISE.GetNoise3D(globalBlockPosition.X*Instance._scalefactor, globalBlockPosition.Y*Instance._scalefactor, globalBlockPosition.Z*Instance._scalefactor);
                            var noiseabove = NOISE.GetNoise3D(globalBlockPosition.X*Instance._scalefactor, globalBlockPosition.Y*Instance._scalefactor+1, globalBlockPosition.Z*Instance._scalefactor);
                            var noisebelow = NOISE.GetNoise3D(globalBlockPosition.X*Instance._scalefactor, globalBlockPosition.Y*Instance._scalefactor-1, globalBlockPosition.Z*Instance._scalefactor);
                            if (noise3d*(1.0f-(globalBlockPosition.Y/58.0f))+0.001 >= Instance._cutoff)
                            {
                                if (noiseabove < Instance._cutoff) {
                                    blockType = rnd.Randf() > 0.99 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Grass");
                                }
                                else if (noisebelow > noise3d) blockType = BlockManager.BlockID("Stone");
                                else blockType = BlockManager.BlockID("Dirt");
                            }
                        }
                    
                        // add the damage type 
                        result[block_idx] = PackAllBlockInfo(blockType,0,0,0,0,0);

                        if (!IsBlockEmpty(result[block_idx])) filledBlocks.Add(new Vector3I(x, y, z));
                    }
                }
            }
        }

        // loop over all filled blocks again, add slopes
        //Dictionary<Vector3I,int> packedSlopeBlockData = new();

        

        // add corner and side slopes
        // corner slopes should make the block below the same type
        // we need to set the result first because neighbour checks the concurrent dictionary

        //var filledexclude = filledBlocks.Where(p => (p.Y > 0 && p.Y < CSP-1)&&(p.X > 0 &&p.X < CSP-1)&&(p.Z > 0 && p.Z <CSP-1) ).ToList();
        Instance.BLOCKCACHE[chunkPosition] = result;
        result = BatchUpdateBlockSlopeData(chunkPosition, filledBlocks, result);
        Instance.BLOCKCACHE[chunkPosition] = result;

        Instance.DeferredMeshUpdates[chunkPosition] = filledBlocks;
	}

    public static float TerraceFunc(float x) {
        return Mathf.Pow(Mathf.Sin((x - Mathf.Round(x)) * 2.45f),11) + Mathf.Round(x);
    }

    #endregion


    // greedy chunk mesh both meshes the chunk, and also adds neighbouring blocks to the chunk in the block cache
    #region GreedyChunkMesh
    public static void GreedyChunkMesh(Dictionary<int, Dictionary<int, UInt32[]>>[] data, Vector3I chunk_index, int subchunk, List<Vector3I> filledBlocks = null) {
        // we expect blocks generated before attempting to mesh
        if (!Instance.BLOCKCACHE.TryGetValue(chunk_index, out var chunk_blocks)) return;

        var axis_cols = new UInt32[CSP3*3];
        var col_face_masks = new UInt32[CSP3*6];
        var slope_blocks = new Dictionary<int, UInt32[]>();

        // method which 
        int dx, dy, dz;
        dx = dy = dz = 0;
        var delta = new Vector3I(dx,dy,dz);
        var prev_delta = delta;
        var targ_chunk = chunk_blocks;
        int modify_chunk_blocks(int x, int y, int z) {
            // read padded blocks from neighboring chunks
            prev_delta = delta;
            dx = x > CHUNK_SIZE ? 1 : x < 1 ? -1 : 0;
            dy = y > CHUNK_SIZE ? 1 : y < 1 ? -1 : 0;
            dz = z > CHUNK_SIZE ? 1 : z < 1 ? -1 : 0;
            delta = new Vector3I(dx,dy,dz);
            
            if (prev_delta != delta) {
                if (!Instance.BLOCKCACHE.TryGetValue(chunk_index+delta, out var new_chunk)) {
                    new_chunk = chunk_blocks;
                }
                targ_chunk = new_chunk;
            }

            var islocal = targ_chunk==chunk_blocks;

            var block_pos = new Vector3I(x,y,z) - (islocal ? Vector3I.Zero : delta*CHUNK_SIZE);
            var idx = BlockIndex(block_pos);
            idx += subchunk*CSP3; // move up one subchunk
            var blockinfo = targ_chunk[idx];

            // HACK set blockinfo to zero to prevent sloped air blocks bug
            if (IsBlockEmpty(blockinfo)) blockinfo = 0;
            if (islocal)
                chunk_blocks[BlockIndex(block_pos)] = blockinfo;

            return blockinfo;
        }

        // populate from neighbouring chunks
        for (int x=0;x<CSP;x+=CSP-1) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z++) {
                    modify_chunk_blocks(x,y,z);
                }
            }
        }
        dx = dy = dz = 0;
        delta = new Vector3I(dx,dy,dz);
        prev_delta = delta;
        targ_chunk = chunk_blocks;
        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y+=CSP-1) {
                for (int z=0;z<CSP;z++) {
                    modify_chunk_blocks(x,y,z);
                }
            }
        }
        dx = dy = dz = 0;
        delta = new Vector3I(dx,dy,dz);
        prev_delta = delta;
        targ_chunk = chunk_blocks;
        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z+=CSP-1) {
                    modify_chunk_blocks(x,y,z);
                }
            }
        }
        
        // update chunk slope data if we have a list for it
        if (filledBlocks == null)
        {
            if (Instance.DeferredMeshUpdates.TryGetValue(chunk_index, out var deferredFilledBlocks))
            {
                chunk_blocks = BatchUpdateBlockSlopeData(chunk_index, deferredFilledBlocks, chunk_blocks, true);
            }
        }
        else
        {
            // the only time filledBlocks is not null is when we damage an already generated chunk
            // in this case, we want to ignore slopes = true so existing slopes dont revert back into whole blocks
            if (filledBlocks.Count > 0)
                chunk_blocks = BatchUpdateBlockSlopeData(chunk_index, filledBlocks, chunk_blocks, true);
        }

        // generate binary 0 1 voxel representation for each axis
        // central chunk loop
        dx = dy = dz = 0;
        delta = new Vector3I(dx,dy,dz);
        prev_delta = delta;
        targ_chunk = chunk_blocks;
        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z++) {
                    var blockinfo = modify_chunk_blocks(x,y,z);

                    if (IsBlockSloped(blockinfo)) {
                        if (dx != 0 || dy != 0 || dz != 0) continue;  // dont add sloped blocks if we are in padded space
                        // add sloped blocks and IDs to a separate list
                        var idx = BlockIndex(new Vector3I(x,y,z));
                        if (!slope_blocks.TryGetValue(idx, out _ )) {
                            slope_blocks.Add(idx, new UInt32[] {(uint)blockinfo});
                        }
                    }
                    else if (!IsBlockEmpty(blockinfo)) { // if block is solid
                        axis_cols[x + z*CSP] |= (UInt32)1 << y;           // y axis defined by x,z
                        axis_cols[z + y*CSP + CSP2] |= (UInt32)1 << x;    // x axis defined by z,y
                        axis_cols[x + y*CSP + CSP2*2] |= (UInt32)1 << z;  // z axis defined by x,y
                    }
                }
            }
        }

        // add slope blocks to entry zero of the extra "axis"
        // data 1-5 are the cube axes, 6 is the sloped blocks 
        data[6].Add(0, slope_blocks);

        // do face culling for each axis
        for (int axis = 0; axis < 3; axis++) {
            for (int i=0; i<CSP2; i++) {
                var col = axis_cols[i + axis*CSP2];
                // sample descending axis and set true when air meets solid
                col_face_masks[CSP2*axis*2 + i] = col & ~(col << 1);
                // sample ascending axis and set true when air meets solid
                col_face_masks[CSP2*(axis*2+1) + i] = col & ~(col >> 1);
            }
        }

        // put the data into the hash maps
        for (int axis = 0; axis < 6; axis++) {
            // i and j are coords in the binary plane for the given axis
            // i is column, j is row
            for (int j=0;j<CHUNK_SIZE;j++) {
                for (int i=0;i<CHUNK_SIZE;i++) {
                    // get column index for col_face_masks
                    // add 1 to i and j because we are skipping the first row and column due to padding
                    var col_idx = (i+1) + ((j+1) * CSP) + (axis * CSP2);

                    // removes rightmost and leftmost padded bit (it's outside the chunk)
                    var col = col_face_masks[col_idx] >> 1;
                    col &= ~((UInt32)1 << CHUNK_SIZE);

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
                        var blockinfo = chunk_blocks[BlockIndex(voxel_pos+Vector3I.One)];
                        
                        if (!data[axis].TryGetValue(blockinfo, out Dictionary<int, UInt32[]> planeSet)) {
                            planeSet = new(); 
                            data[axis].Add(blockinfo, planeSet);
                        }

                        var k_ymod = k+Dimensions.Y*subchunk;
                        if (!planeSet.TryGetValue(k_ymod, out UInt32[] data_entry)) {
                            data_entry = new UInt32[CHUNK_SIZE];
                            planeSet.Add(k_ymod, data_entry);
                        }
                        data_entry[j] |= (UInt32)1 << i;     // push the "row" bit into the "column" UInt32
                        planeSet[k_ymod] = data_entry;
                    }
                }
            }
        }
    }
    #endregion

    #region BuildChunkMesh
    public static ChunkMeshData BuildChunkMesh(Vector3I chunk_index, List<Vector3I> filledBlocks = null) {


        static int get_surface_tool_index(int blockinfo, int axis) {
            var blockId = GetBlockID(blockinfo);
            if (blockId == BlockManager.Instance.LavaBlockId)
            {
                return ChunkMeshData.LAVA_SURFACE;
            }
            else if (blockId == BlockManager.BlockID("GoldOre"))
            {
                return ChunkMeshData.GOLD_SURFACE;
            }
            else if (blockId == BlockManager.BlockID("Grass") && axis == 1)
            {
                return ChunkMeshData.GRASS_SURFACE;
            }
            else
            {
                return ChunkMeshData.CHUNK_SURFACE;
            }
        }

        // data is an array of dictionaries, one for each axis
        // each dictionary is a hash map of block types to a set binary planes
        // we need to group by block type like this so we can batch the meshing and texture blocks correctly
        Dictionary<int, Dictionary<int, UInt32[]>>[] data = new Dictionary<int,Dictionary<int, UInt32[]>>[7];
        short i;
        for (i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value
        data[i] = new(); // an extra one for sloped blocks

        // add all SUBCHUNKS
        for (i=0; i < SUBCHUNKS; i++) GreedyChunkMesh(data, chunk_index, i, filledBlocks);

        // construct mesh
        var surfToolArray = new SurfaceTool[ChunkMeshData.ALL_SURFACES];
        for (var s=0; s<ChunkMeshData.ALL_SURFACES; s++) {
            surfToolArray[s] = new();
            surfToolArray[s].Begin(Mesh.PrimitiveType.Triangles);
        } 

        for (int axis=0; axis<6;axis++) {
            foreach (var (blockinfo, planeSet) in data[axis]) {
                foreach (var (k_chunked, binary_plane) in planeSet) {
                    var blockId = GetBlockID(blockinfo);

                    // sloped blocks are not greedy meshed
                    var greedy_quads = GreedyMeshBinaryPlane(binary_plane);

                    var k = k_chunked % Dimensions.Y;
                    var subchunk = k_chunked/Dimensions.Y;

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
                        quad_offset += Vector3I.Up*subchunk*Dimensions.Y;

                        // construct vertices and normals for mesh
                        Godot.Vector3[] verts = new Godot.Vector3[4];
                        for (i=0; i<4; i++) {
                            verts[i] = quad_offset + (Godot.Vector3)CUBE_VERTS[CUBE_AXIS[axis,i]]*quad_delta;
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

                        var surfidx = get_surface_tool_index(blockinfo, axis);
                        if (surfidx == ChunkMeshData.LAVA_SURFACE)
                        {
                            // lava surface has no metadata
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, normals: normals);
                            surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, normals: normals);
                        }
                        else
                        {
                            var blockDamage = GetBlockDamageData(blockinfo);
                            var block_face_texture_idx = BlockManager.BlockTextureArrayPositions(blockId)[axis];
                            var notacolour = new Color(block_face_texture_idx, uv_offset.X, uv_offset.Y, blockDamage)*(1/255f);
                            var metadata = new Color[] {notacolour, notacolour, notacolour};
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                    }
                }
            }
        }

        #region sloped blocks
        // sloped blocks are not greedy meshed, but constucted seperately
        // their data is stored in the 7th dictionary
        foreach (var (block_idx, blockdata) in data[6][0])
        {
            var blockinfo = (int)blockdata[0];
            var blockId = GetBlockID(blockinfo);
            var slopeType = GetBlockSlopeType(blockinfo);
            var flipSlope = GetBlockSlopeFlip(blockinfo);

            // two types of slope, regular slope (id:1) or angled (7 face) corner slope (id:2)
            // all blocks in this set are sloped so it's either going to be 1 or 2
            var regularSlope = slopeType == (int)SlopeType.Side;
            var cornerSlope = slopeType == (int)SlopeType.Corner;
            var invCornerSlope = slopeType == (int)SlopeType.InvCorner;
            float rotation_angle = GetBlockSlopeRotation(blockinfo);
            //rotation_angle += Mathf.Pi/2;
            while (rotation_angle > Mathf.Pi*2) rotation_angle -= Mathf.Pi*2;
            // DEBUG no flip slope
            //if (flipSlope && !regularSlope) rotation_degrees -= 90f;
        /*
            var x = chunk_idx % CHUNK_SIZE;
            var z = (chunk_idx / CHUNK_SIZE) % CHUNK_SIZE;
            var y = chunk_idx / CHUNKSQ;*/
            Vector3I pos = BlockIndexToVector(block_idx);//new(x,y,z);//BlockIndexToVector(chunk_idx);
            pos -= Vector3I.One; // remove padding

            for (int axis=0;axis<6;axis++)
            {
                // regular slope - skip front face because it's a ramp
                if (regularSlope && axis==4) continue;

                //pos += quad_offset;

                var blockDamage = GetBlockDamageData(blockinfo);
                var block_face_texture_idx = BlockManager.BlockTextureArrayPositions(blockId)[axis];
                var notacolour = new Color(block_face_texture_idx, 1.0f, 1.0f, blockDamage)*(1/255f);
                var metadata = new Color[] {notacolour, notacolour, notacolour};

                Godot.Vector3[] verts = new Godot.Vector3[4];

                for (i=0; i<4; i++) {
                    // get local vertex coords
                    verts[i] = (Godot.Vector3) CUBE_VERTS[CUBE_AXIS[axis,i]] - Godot.Vector3.One * 0.5f;

                    // shift down top face into a slope, for regular slope
                    if (regularSlope && axis==1 && (i==0 || i==1)) verts[i] -= Godot.Vector3.Up;
                    if (cornerSlope && axis==1 && (i==0 || i==1 || i ==2)) verts[i] -= Godot.Vector3.Up; // else shift corner down by 1 for corner slopes
                    if (invCornerSlope && axis==1 && i==1) verts[i] -= Godot.Vector3.Up; // else shift corner down by 1
                    
                    
                    verts[i] = verts[i].Rotated(Godot.Vector3.Up, rotation_angle);
                    if (flipSlope) verts[i] = verts[i].Rotated(Godot.Vector3.Forward, Mathf.Pi);
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

                var surfidx = get_surface_tool_index(blockinfo, axis);
                switch (axis)
                {
                    case 1: // top face - modify normals
                        if (invCornerSlope) surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        
                        var normrotate = SlopedNormalNegZ;
                        if (cornerSlope || invCornerSlope) normrotate = SlopedCornerNormalNegZ;
                        if (flipSlope) normrotate = normrotate.Rotated(Godot.Vector3.Forward, Mathf.Pi);
                        normrotate = normrotate.Rotated(Godot.Vector3.Up, rotation_angle);
                        normals = new Godot.Vector3[] {normrotate,normrotate,normrotate};
                        if (regularSlope || invCornerSlope) surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);

                        if (!invCornerSlope) surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        break;
                    case 2: // side face, only add one of the triangles
                        if (regularSlope || cornerSlope) surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        else if (invCornerSlope) {
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals); 
                        }
                        break;
                    case 3: // obverse side face, only add one of the triangles and adjust its vertices accordingly
                        triangle1 = new Godot.Vector3[] {verts[1], verts[2], verts[3]};

                        //if (invCornerSlope) uvTriangle1 = new Godot.Vector2[] { uvC, uvB, uvA };
                        if (regularSlope || invCornerSlope) {
                            uvTriangle1 = new Godot.Vector2[] { uvC, uvB, uvA };
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        }
                        break;
                    case 4: // facing -z, front, corner slopes only add one triangle, else normal
                        if (regularSlope) {
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                        else if (invCornerSlope) surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        break;
                    case 5:
                        if (regularSlope || invCornerSlope) {
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                        if (cornerSlope) {
                            triangle1 = new Godot.Vector3[] {verts[1], verts[2], verts[3]};
                            uvTriangle1 = new Godot.Vector2[] { uvC, uvB, uvA };
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        }
                        break;
                    default: // bottom face is always drawn, corner slopes only have 1 triangle
                        if (cornerSlope)
                        {
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        }
                        else
                        {
                            surfToolArray[surfidx].AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            surfToolArray[surfidx].AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                        break;
                }
            }
        }
        #endregion

        // index grass surface
        surfToolArray[ChunkMeshData.GRASS_SURFACE].Index();        
        var surfaces = new ArrayMesh[ChunkMeshData.ALL_SURFACES];
        surfaces[ChunkMeshData.CHUNK_SURFACE] = surfToolArray[ChunkMeshData.CHUNK_SURFACE].Commit();
        surfaces[ChunkMeshData.LAVA_SURFACE] = surfToolArray[ChunkMeshData.LAVA_SURFACE].Commit();
        surfaces[ChunkMeshData.GRASS_SURFACE] = surfToolArray[ChunkMeshData.GRASS_SURFACE].Commit();
        surfaces[ChunkMeshData.GOLD_SURFACE] = surfToolArray[ChunkMeshData.GOLD_SURFACE].Commit();

        int playerChunkX, playerChunkZ; //playerChunkY

        //Godot.Vector3 player_glob_pos;

        lock(Instance._playerPositionLock)
        {
            playerChunkX = Mathf.FloorToInt(Instance._playerPosition.X / (Dimensions.X*VOXEL_SCALE));
            //playerChunkY = Mathf.FloorToInt(_playerPosition.Y / (Dimensions.Y*SUBCHUNKS*Chunk.VOXEL_SCALE));
            //playerChunkY = Mathf.FloorToInt((_playerPosition.Y+Chunk.VOXEL_SCALE*Dimensions.Y*SUBCHUNKS*0.5f) / (Dimensions.Y*SUBCHUNKS*Chunk.VOXEL_SCALE));
            playerChunkZ = Mathf.FloorToInt(Instance._playerPosition.Z / (Dimensions.Z*VOXEL_SCALE));
            
            //player_glob_pos = _playerPosition;
        }

        // TODO set mesh LOD collision shapes
        var noCollisions = false;//new Vector3I(chunk_index.X,0,chunk_index.Z).DistanceSquaredTo(new Vector3I(playerChunkX, 0, playerChunkZ)) > (8*8);

        return new ChunkMeshData(surfaces, noCollisions);
    }
    #endregion

    #region GreedyMeshBinaryPlane
    // greedy quad for a 32 x 32 binary plane (assuming data length is 32) // CHANGED THIS TO 64 CHUNK SIZE
    // each Uint32 in data[] is a row of 32 bits
    // offsets along this row represent columns
    private static List<GreedyQuad> GreedyMeshBinaryPlane(UInt32[] data) { // modify this so chunks are 30 and padded 1 on each side to 32
        List<GreedyQuad> greedy_quads = new();
        int data_length = data.Length;
        for (int j=0;j<data_length;j++) { // j selects a row from the data[j]
            var i = 0; // i  traverses the bits in current row j
            while (i < CHUNK_SIZE) {
                i += BitOperations.TrailingZeroCount(data[j] >> i);
                if (i>=CHUNK_SIZE) continue;
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
    #endregion
}