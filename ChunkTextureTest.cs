using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

[Tool]
public partial class ChunkTextureTest : Node3D
{
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

    private Vector3I _prevChunkPosition = new(int.MinValue,int.MinValue,int.MinValue);

    public static readonly ArrayMesh GrassBladeFullRes = ResourceLoader.Load("res://shaders/grass/blade.res") as ArrayMesh;
    public static readonly ArrayMesh GrassBladeLowRes = ResourceLoader.Load("res://shaders/grass/blade_optimized.tres") as ArrayMesh;

    public static readonly PackedScene GrassParticlesScene = ResourceLoader.Load("res://shaders/grass/grass_particles.tscn") as PackedScene;

    public static readonly ShaderMaterial GrassBladeMaterial = ResourceLoader.Load("res://shaders/grass/multimesh_grass_shader.tres") as ShaderMaterial;

    [Export] public Grass GrassMultiMesh {get; set;}

    [Export] public Godot.Vector3 PlayerPosition {get; set;}

    public override void _Process(double delta) {
        var chunkpos = (Vector3I)ChunkMesh.GlobalPosition;
        if (_prevChunkPosition != chunkpos && ChunkMesh != null && ChunkMesh is MeshInstance3D mesh && BlockManager.Instance != null) {
            var blocks = GenerateTest(chunkpos);
            var chunkmeshdata = BuildChunkMeshTest(blocks, false);
            mesh.Mesh = chunkmeshdata.UnifySurfaces();

            if (chunkmeshdata.HasSurfaceOfType(ChunkMeshData.GRASS_SURFACE)) {
                var grass_mesh = chunkmeshdata.GetSurface(ChunkMeshData.GRASS_SURFACE);
                GrassMultiMesh.TerrainMesh = grass_mesh;
                GrassMultiMesh.MaterialOverride = GrassBladeMaterial;
                //GD.Print($"set terrain mesh to {GrassMultiMesh.TerrainMesh}");
            }

            _prevChunkPosition = chunkpos;
        }
    }

public static int[] GenerateTest(Vector3I chunkPosition)
	{
        var result = new int[Chunk.CHUNKSQ*Chunk.CHUNK_SIZE*Chunk.SUBCHUNKS];
        var rnd = new RandomNumberGenerator();

        // blocks spawn when 3d noise is >= cutoff (its values are -1 to 1)
        var cutoff = 0.2f;
        
        for (int subchunk = 0; subchunk < Chunk.SUBCHUNKS; subchunk++) {
            for (int x=0;x<Chunk.CHUNK_SIZE;x++) {
                for (int y=0;y<Chunk.CHUNK_SIZE;y++) {
                    for (int z=0;z<Chunk.CHUNK_SIZE;z++) {
                        int block_idx = x + y * Chunk.CHUNKSQ + z * Chunk.CHUNK_SIZE + subchunk*Chunk.CHUNKSQ*Chunk.CHUNK_SIZE;
                        if (block_idx >= result.Length) continue;  

                        var globalBlockPosition = chunkPosition * new Vector3I(Chunk.Dimensions.X, Chunk.Dimensions.Y*Chunk.SUBCHUNKS, Chunk.Dimensions.Z)
                            + new Vector3I(x, y + Chunk.Dimensions.Y*subchunk, z);                 

                        int blockType = 0;
                        
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
                                    }
                                }
                                continue;
                            }
                            if (y>=3) continue;

                            // apply damage to upper layer blocks
                            int _damamount = 0, _damtype = 0;
                            if (rnd.Randf() < 0.5)
                            {
                                _damtype = rnd.RandiRange(1, 7);
                                _damamount = rnd.RandiRange(0, 31);
                            }
                            var _daminfo = (_damtype<<5) | _damamount;

                            result[block_idx] = Chunk.PackBlockInfo(blockType) | _daminfo;
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
                                if (noiseabove < cutoff || y==Chunk.CHUNK_SIZE-1) blockType = rnd.Randf() > 0.99 ? BlockManager.BlockID("GoldOre") : BlockManager.BlockID("Grass");
                                else if (noisebelow > noise3d) blockType = BlockManager.BlockID("Stone");
                                else blockType = BlockManager.BlockID("Dirt");
                            }
                        }

                        // add the damage type 
                        var blockinfo = Chunk.PackBlockInfo(blockType);
                        result[block_idx] = blockinfo;
                    }
                }
            }
        }

        return result;
	}

    public struct ChunkMeshData {
        public const byte MAX_SURFACES = 3;
        public const byte CHUNK_SURFACE = 0;
        public const byte GRASS_SURFACE = 1;
        public const byte LAVA_SURFACE = 2;
        private ArrayMesh[] _surfaces;

        public ChunkMeshData(ArrayMesh[] input_surfaces) {
            _surfaces = new ArrayMesh[MAX_SURFACES];
            _surfaces[CHUNK_SURFACE] = input_surfaces[CHUNK_SURFACE];
            _surfaces[GRASS_SURFACE] = input_surfaces[GRASS_SURFACE];
            _surfaces[LAVA_SURFACE] = input_surfaces[LAVA_SURFACE];
        }

        public readonly ArrayMesh UnifySurfaces() {
            var _arraymesh = new ArrayMesh();
            for (byte type =0 ; type < MAX_SURFACES ; type ++) {
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

    public static ChunkMeshData BuildChunkMeshTest(int[] chunk_blocks, bool isLowestChunk) {
        // data is an array of dictionaries, one for each axis
        // each dictionary is a hash map of block types to a set binary planes
        // we need to group by block type like this so we can batch the meshing and texture blocks correctly
        Dictionary<int, Dictionary<int, UInt32[]>>[] data = new Dictionary<int,Dictionary<int, UInt32[]>>[6];
        for (short i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value

        // add all Chunk.SUBCHUNKS
        for (int i=0; i< Chunk.SUBCHUNKS; i++) GreedyChunkMeshTest(data, chunk_blocks, i);

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
                        for (int i=0; i<4; i++) {
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
                        var blockId = Chunk.GetBlockID(blockinfo);
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

        
        grassTopSurfaceTool.Index();
        var a1 = _st.Commit();
        var a2 = _st2.Commit();
        var a3 = grassTopSurfaceTool.Commit();
        
        var surfaces = new ArrayMesh[ChunkMeshData.MAX_SURFACES];
        surfaces[ChunkMeshData.CHUNK_SURFACE] = a1;
        surfaces[ChunkMeshData.LAVA_SURFACE] = a2;
        surfaces[ChunkMeshData.GRASS_SURFACE] = a3;

        var cmd = new ChunkMeshData(surfaces);

        return new ChunkMeshData(surfaces);
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

        // generate binary 0 1 voxel representation for each axis
        for (int x=0;x<Chunk.CSP;x++) {
            for (int y=0;y<Chunk.CSP;y++) {
                for (int z=0;z<Chunk.CSP;z++) {
                    var pos = new Vector3I(x,y,z)-Vector3I.One; // goofy ahh check for out of bounds
                    if (pos.X<0||pos.X>=Chunk.CHUNK_SIZE||pos.Y<0||pos.Y>=Chunk.CHUNK_SIZE||pos.Z<0||pos.Z>=Chunk.CHUNK_SIZE) continue; 
                    var chunk_idx = pos.X + pos.Z*Chunk.CHUNK_SIZE + pos.Y*Chunk.CHUNKSQ;
                    chunk_idx += subchunk*Chunk.CHUNKSQ*Chunk.CHUNK_SIZE; // move up one subchunk
                    
                    var b = chunk_blocks[chunk_idx];
                    if (!Chunk.IsBlockEmpty(b)) { // if block is solid
                        axis_cols[x + z*Chunk.CSP] |= (UInt32)1 << y;           // y axis defined by x,z
                        axis_cols[z + y*Chunk.CSP + Chunk.CSP2] |= (UInt32)1 << x;    // x axis defined by z,y
                        axis_cols[x + y*Chunk.CSP + Chunk.CSP2*2] |= (UInt32)1 << z;  // z axis defined by x,y
                    }
                }
            }
        }

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