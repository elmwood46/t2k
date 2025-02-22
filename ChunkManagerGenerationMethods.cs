using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

public partial class ChunkManager : Node {

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

public struct ChunkMeshData {
        public const byte MAX_SURFACES = 3;
        public const byte CHUNK_SURFACE = 0;
        public const byte GRASS_SURFACE = 1;
        public const byte LAVA_SURFACE = 2;
        private readonly ArrayMesh[] _surfaces;

        public ChunkMeshData(ArrayMesh[] input_surfaces) {
            _surfaces = new ArrayMesh[MAX_SURFACES];
            _surfaces[CHUNK_SURFACE] = input_surfaces[CHUNK_SURFACE];
            _surfaces[GRASS_SURFACE] = input_surfaces[GRASS_SURFACE];
            _surfaces[LAVA_SURFACE] = input_surfaces[LAVA_SURFACE];
        }

        public readonly ArrayMesh UnifySurfaces() {
            var _arraymesh = new ArrayMesh();
            for (byte type = 0 ; type < MAX_SURFACES ; type ++) {
                if (HasSurfaceOfType(type)) {
                    var surface = _surfaces[type];
                    var _surfmat = type switch {
                        CHUNK_SURFACE => BlockManager.Instance.ChunkMaterial,
                        GRASS_SURFACE => BlockManager.Instance.ChunkMaterial,
                        LAVA_SURFACE => BlockManager.Instance.LavaShader,
                        _ => BlockManager.Instance.ChunkMaterial
                    };
                    _arraymesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surface.SurfaceGetArrays(0));
                    _arraymesh.SurfaceSetMaterial(_arraymesh.GetSurfaceCount()-1, _surfmat);
                }
            }
            return _arraymesh;
        }

        public readonly bool HasSurfaceOfType(byte type) {
            return _surfaces[type].GetSurfaceCount() > 0;
        }

        public readonly ArrayMesh GetSurface(byte type) {
            return _surfaces[type];
        }
    }

#region generate
    public void Generate(Vector3I chunkPosition)
	{
        if (CantorPairing.Contains(chunkPosition)) {
            //GD.Print("cantor pairing says Chunk already generated at ", chunkPosition);
            return;
        }
       // else GD.Print("Generating chunk at ", chunkPosition);

        if (!ChunkCache.TryGetValue(chunkPosition, out var result)) {
            result = new int[CSP2*CSP*SUBCHUNKS];
            ChunkCache.TryAdd(chunkPosition, result);
        }

        var rnd = new RandomNumberGenerator();

        // blocks spawn when 3d noise is >= cutoff (its values are -1 to 1)
        var cutoff = 0.2f;

        List<Vector3I> filledBlocks = new();

        var scalefactor = 0.5f;
        
        for (int subchunk = 0; subchunk < SUBCHUNKS; subchunk++) {
            for (int x=0;x<CSP;x++) {
                for (int y=0;y<CSP;y++) {
                    for (int z=0;z<CSP;z++) {
                        int block_idx = x + y * CSP2 + z * CSP + subchunk*CSP3;
                        if (block_idx >= result.Length) continue;

                        var globalBlockPosition = chunkPosition * new Vector3I(Dimensions.X, Dimensions.Y*SUBCHUNKS, Dimensions.Z)
                            + new Vector3I(x, y + CSP2*subchunk, z) - Vector3I.One;

                        int blockType = result[block_idx];
                        
                        // generate highest level differently
                        if (chunkPosition.Y == 5)
                        {
                            var noise3d = NOISE.GetNoise3D(scalefactor*globalBlockPosition.X, scalefactor*globalBlockPosition.Y, scalefactor*globalBlockPosition.Z);
                            if (y == 0 && noise3d >= cutoff) 
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
                                result[block_idx-CSP2] = PackBlockInfo(BlockManager.BlockID("Dirt"));
                                var blockSet = rnd.Randf() > 0.5 ? GenStructure.GenerateTotem(rnd.RandiRange(3,15)) : GenStructure.GenerateTree();

                                // blockset is a dictionary of block positions and block info ints (with all bits initialized)
                                foreach (KeyValuePair<Vector3I, int> kvp in blockSet)
                                {
                                    Vector3I p = new(x + kvp.Key.X, y + kvp.Key.Y, z + kvp.Key.Z);

                                    if (p.X < CSP && p.X >= 0
                                    &&  p.Y < CSP && p.Y >= 0
                                    &&  p.Z < CSP && p.Z >= 0)
                                    {
                                        if (IsBlockEmpty(kvp.Value)) continue;
                                        result[p.X + p.Y*CSP2 + p.Z*CSP] = kvp.Value;
                                        filledBlocks.Add(new Vector3I(p.X, p.Y, p.Z));

                                        // randomly damage some blocks, but not leaves
                                        //GetBlockSpecies(kvp.Value) == BlockSpecies.Leaves
                                        if (p.Y>=3) continue;
                                        var dam_amount = 0;
                                        var dam_type = 0;
                                        if (rnd.Randf() < 0.5)
                                        {
                                            dam_type = rnd.RandiRange(1, 7);
                                            dam_amount = rnd.RandiRange(0, 31);
                                        }
                                        var dam_info = PackBlockDamageInfo(dam_type, dam_amount);

                                        // add the damage type 
                                        result[p.X + p.Y*CSP2 + p.Z*CSP] |= dam_info;
                                    } else {
                                        var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
                                        var dx = p.X > CSP-1 ? 1 : p.X < 0 ? -1 : 0;
                                        var dy = p.Y > CSP-1 ? 1 : p.Y < 0 ? -1 : 0;
                                        var dz = p.Z > CSP-1 ? 1 : p.Z < 0 ? -1 : 0;
                                        var delta = new Vector3I(dx, dy, dz);
                                        var newp = (p+delta)-delta*CSP;
                                        var idx = newp.X + newp.Y*CSP2 + newp.Z*CSP;
                                        if (!ChunkCache.TryGetValue(neighbour_chunk+delta, out var neighbour)) {
                                            neighbour = new int[CSP2*CSP*SUBCHUNKS];
                                            neighbour[idx] = kvp.Value;
                                            ChunkCache.TryAdd(neighbour_chunk+delta,neighbour);
                                        } else {
                                            if (IsBlockEmpty(neighbour[idx])) neighbour[idx] = kvp.Value;
                                            ChunkCache.TryRemove(neighbour_chunk+delta,out var _);
                                            ChunkCache.TryAdd(neighbour_chunk+delta,neighbour);
                                        }
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
                            var _daminfo = PackBlockDamageInfo(_damtype, _damamount);
                            
                            result[block_idx] = PackBlockInfo(blockType) | _daminfo;

                            if (blockType != 0) filledBlocks.Add(new Vector3I(x, y, z));

                            continue;
                        }
                        else if (chunkPosition.Y == 4) {
                            var noise3d = NOISE.GetNoise3D(globalBlockPosition.X, globalBlockPosition.Y, globalBlockPosition.Z);
                            if (y == 0 && noise3d >= cutoff) 
                            {
                                blockType = BlockManager.BlockID("Check2");
                            } 
                            else if (y > 0 && block_idx-CSP2 > 0 && !IsBlockEmpty(result[block_idx-CSP2]))
                            {
                                blockType = BlockManager.BlockID("Check2");
                            }
                        }

                        // generate other levels
                        var noise = CELLNOISE.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Z);
                        var groundheight = (int)(1*(noise+1)/2);
                        if (chunkPosition.Y == 0)
                        {
                            if (y<2) blockType = BlockManager.Instance.LavaBlockId;
                            else if (globalBlockPosition.Y == groundheight) blockType = rnd.Randf() > 0.99 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Grass");
                            else if (globalBlockPosition.Y > groundheight - 3) blockType = BlockManager.BlockID("Dirt");
                            else blockType = rnd.Randf() > 0.9 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Stone");
                        }
                        else if (chunkPosition.Y != 4 && chunkPosition.Y != 6)
                        {
                            //var height = TerraceFunc(globalBlockPosition.Y*scalefactor);
                            var noise3d = NOISE.GetNoise3D(globalBlockPosition.X*scalefactor, globalBlockPosition.Y*scalefactor, globalBlockPosition.Z*scalefactor);
                            var noiseabove = NOISE.GetNoise3D(globalBlockPosition.X*scalefactor, globalBlockPosition.Y*scalefactor+1, globalBlockPosition.Z*scalefactor);
                            var noisebelow = NOISE.GetNoise3D(globalBlockPosition.X*scalefactor, globalBlockPosition.Y*scalefactor-1, globalBlockPosition.Z*scalefactor);
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
                        var blockinfo = PackBlockInfo(blockType);
                        result[block_idx] = blockinfo;

                        if (blockType != 0) filledBlocks.Add(new Vector3I(x, y, z));
                    }
                }
            }
        }

        // loop over all filled blocks again, add slopes
        Dictionary<Vector3I,int> packedSlopeBlockData = new();

        // add corner and side slopes
        // corner slopes should make the block below the same type
        foreach (var p in filledBlocks) {
            var i = p.X + p.Y*CSP2 + p.Z*CSP;
            if (!IsBlockEmpty(result[i]) && GetBlockSpecies(result[i])!=BlockSpecies.Leaves && BlockIsSlopeCandidate(chunkPosition, p)) {
                var s = BlockPackSlopeInfo(chunkPosition, p);
                if (GetBlockSlopeType(s) == (int)SlopeType.Corner) {
                    if (GetBlockID(GetBlockNeighbour(chunkPosition, p, Vector3I.Down))!=BlockManager.Instance.LavaBlockId)
                        SetBlockNeighbourIfNotEmpty(chunkPosition, p, Vector3I.Down, result[i]); 
                }
                packedSlopeBlockData[p] = s;
            }
        }

        foreach (var (p, packedSlopeData) in packedSlopeBlockData) {
            var i = p.X + p.Y*CSP2 + p.Z*CSP;
            result[i] |= packedSlopeData;
        }

        if (ChunkCache.TryRemove(chunkPosition, out var _)) 
        {
            ChunkCache.TryAdd(chunkPosition, result);
        }

        CantorPairing.Add(chunkPosition);
	}

    public static float TerraceFunc(float x) {
        return Mathf.Pow(Mathf.Sin((x - Mathf.Round(x)) * 2.45f),11) + Mathf.Round(x);
    }

    #endregion

    #region GreedyChunkMesh
    public static void GreedyChunkMesh(Dictionary<int, Dictionary<int, UInt32[]>>[] data, int[] chunk_blocks_padded, int subchunk) {
        var chunk_blocks = new int[CHUNKSQ*CHUNK_SIZE];
        for (int x=1;x<=CHUNK_SIZE;x++) {
            for (int y=1;y<=CHUNK_SIZE;y++) {
                for (int z=1;z<=CHUNK_SIZE;z++) {
                    var chunk_idx = x-1 + (z-1)*CHUNK_SIZE + (y-1)*CHUNKSQ;
                    if (chunk_idx >= chunk_blocks_padded.Length) continue;
                    var chunk_pad_idx = x + z*CSP + y*CSP2;
                    chunk_blocks[chunk_idx] = chunk_blocks_padded[chunk_pad_idx];
                }
            }
        }

        var axis_cols = new UInt32[CSP3*3];
        var col_face_masks = new UInt32[CSP3*6];
        var slope_blocks = new Dictionary<int, UInt32[]>();

        // generate binary 0 1 voxel representation for each axis
        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z++) {
                    var pos = new Vector3I(x,y,z)-Vector3I.One; // goofy ahh check for out of bounds
                    if (pos.X<0||pos.X>=CHUNK_SIZE||pos.Y<0||pos.Y>=CHUNK_SIZE||pos.Z<0||pos.Z>=CHUNK_SIZE) continue; 
                    var chunk_idx = pos.X + pos.Z*CHUNK_SIZE + pos.Y*CHUNKSQ;
                    chunk_idx += subchunk*CHUNKSQ*CHUNK_SIZE; // move up one subchunk
                    
                    var b = chunk_blocks[chunk_idx];
                    if (IsBlockSloped(b)) {
                        // add sloped blocks and IDs to a separate list
                        if (!slope_blocks.TryGetValue(chunk_idx, out _ )) {
                            slope_blocks.Add(chunk_idx, new UInt32[] {(uint)b});
                        }
                    }
                    else if (!IsBlockEmpty(b)) { // if block is solid
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
                        var blockinfo = chunk_blocks[
                                voxel_pos.X
                                + voxel_pos.Z * CHUNK_SIZE
                                + voxel_pos.Y * CHUNKSQ
                                + subchunk*CHUNKSQ*CHUNK_SIZE
                            ];

                        
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
    public static ChunkMeshData BuildChunkMesh(int[] chunk_blocks) {
        // data is an array of dictionaries, one for each axis
        // each dictionary is a hash map of block types to a set binary planes
        // we need to group by block type like this so we can batch the meshing and texture blocks correctly
        Dictionary<int, Dictionary<int, UInt32[]>>[] data = new Dictionary<int,Dictionary<int, UInt32[]>>[7];
        short i;
        for (i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value
        data[i] = new(); // an extra one for sloped blocks

        // add all SUBCHUNKS
        for (i=0; i < SUBCHUNKS; i++) GreedyChunkMesh(data, chunk_blocks, i);

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

                        // add the quad to the mesh
                        if (blockId == BlockManager.Instance.LavaBlockId)
                        {
                            _st2.AddTriangleFan(triangle1, uvTriangle1, normals: normals);
                            _st2.AddTriangleFan(triangle2, uvTriangle2, normals: normals);
                        }
                        else
                        {
                            var blockDamage = GetBlockDamageData(blockinfo);
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

        #region sloped blocks
        // sloped blocks are not greedy meshed, but constucted seperately
        // their data is stored in the 7th dictionary
        foreach (var (chunk_idx, blockdata) in data[6][0])
        {
            var blockinfo = (int)blockdata[0];
            var blockId = GetBlockID(blockinfo);
            var slopeType = GetBlockSlopeType(blockinfo);

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

            var x = chunk_idx % CHUNK_SIZE;
            var z = (chunk_idx / CHUNK_SIZE) % CHUNK_SIZE;
            var y = chunk_idx / CHUNKSQ;
            Vector3I pos = new(x,y,z); 

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
                    
                    //if (flipSlope) verts[i] = verts[i].Rotated(Godot.Vector3.Forward, Mathf.Pi);
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
                //if (flipSlope) normal = normal.Rotated(Godot.Vector3.Forward, Mathf.Pi);
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
                        //if (flipSlope) normrotate = normrotate.Rotated(Godot.Vector3.Forward, Mathf.Pi);
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
                    default: // bottom face is always drawn, corner slopes only have 1 triangle
                        if (cornerSlope)
                        {
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                        }
                        else
                        {
                            _st.AddTriangleFan(triangle1, uvTriangle1, colors: metadata, normals: normals);
                            _st.AddTriangleFan(triangle2, uvTriangle2, colors: metadata, normals: normals);
                        }
                        break;
                }
            }
        }
        #endregion

        grassTopSurfaceTool.Index();
        var a1 = _st.Commit();
        var a2 = _st2.Commit();
        var a3 = grassTopSurfaceTool.Commit();
        
        var surfaces = new ArrayMesh[ChunkMeshData.MAX_SURFACES];
        surfaces[ChunkMeshData.CHUNK_SURFACE] = a1;
        surfaces[ChunkMeshData.LAVA_SURFACE] = a2;
        surfaces[ChunkMeshData.GRASS_SURFACE] = a3;

        return new ChunkMeshData(surfaces);
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


// block bits
    // byte 1-2, 00-15: block type
    // byte 3,   16-23: block damage info
    // btye 4,   24-31: block slope orientation

    #region block info getters
    public static int PackBlockInfo(int blockType) {
        return blockType;
    }

    public static int PackBlockDamageInfo(int damageType, int damageAmount) {
        return ((damageType<<5) | damageAmount)<<16;
    }

    public static int GetBlockID(int blockInfo) {
        return blockInfo & 0xffff;
    }

    public static int GetBlockDamageData(int blockInfo) {
        return (blockInfo>>16) & 0xff;
    }

    public static int GetBlockDamageInteger(int blockInfo) {
        return GetBlockDamageData(blockInfo)&0x1f;
    }

    public static int GetBlockDamageType(int blockInfo) {
        return GetBlockDamageData(blockInfo) >> 5;
    }

    public static int GetBlockSlopeData(int blockInfo) {
        return blockInfo >> 24;
    }

    public static int GetBlockSlopeType(int blockInfo) {
        return GetBlockSlopeData(blockInfo) & 0b11;
    }

    public static float GetBlockSlopeRotation(int blockInfo) {
        return ((GetBlockSlopeData(blockInfo) >> 2)& 0b11)*Mathf.Pi/2;
    }

    public static int PackSlopeData(int slopeType, int slopeRotation) {
        return ((slopeType) | ((slopeRotation) << 2))<<24;
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
        return GetBlockSlopeType(blockInfo) != 0;
    }

    public static bool IsBlockInvincible(int blockInfo) {
        return IsBlockEmpty(blockInfo) || (GetBlockID(blockInfo) == BlockManager.Instance.LavaBlockId);
    }
    #endregion
}