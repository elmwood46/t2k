using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

[Tool]
public partial class Chunk : StaticBody3D
{
	[Export]
	public CollisionShape3D CollisionShape { get; set; }

	[Export]
	public MeshInstance3D MeshInstance { get; set; }

	public const float VOXEL_SCALE = 0.5f; // chunk space is integer based, so this is the scale of each voxel (and the chunk) in world space
	public const int CHUNK_SIZE = 30; // the chunk size is 62, padded chunk size is 64, // must match size in compute shader
    public const int CHUNKSQ = CHUNK_SIZE*CHUNK_SIZE;
    public const int CSP = CHUNK_SIZE+2;
    public const int CSP2 = CSP*CSP; // squared padded chunk size
    public const int CSP3 = CSP2*CSP; // cubed padded chunk size

	public static readonly Vector3I Dimensions = new(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);

	// obsolete
	private int[,,] _blockHealth = new int[Dimensions.X, Dimensions.Y, Dimensions.Z];

    private ArrayMesh _arraymesh = new();

    private int lavablockid;

	private RandomNumberGenerator rng;
	private SurfaceTool _st = new();
    private SurfaceTool _st2 = new();
	private StandardMaterial3D _material;

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

	// simple 3d binary array for holding blocks 
	private int[] _blocks = new int[CHUNKSQ*CHUNK_SIZE];
	public Vector2I ChunkPosition { get; private set; }

	[Export]
	public FastNoiseLite Noise { get; set; }

	[Export]
	public FastNoiseLite WallNoise { get; set; }

	public void SetChunkPosition(Vector2I position)
	{
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
		CallDeferred(Node3D.MethodName.SetGlobalPosition, new Godot.Vector3(
            VOXEL_SCALE* ChunkPosition.X * Dimensions.X,
            0, VOXEL_SCALE * ChunkPosition.Y * Dimensions.Z)
        );

		Generate();
		Update();
	}

	public override void _Ready() {
        lavablockid = BlockManager.BlockID("Lava");
		Scale = new Godot.Vector3(VOXEL_SCALE, VOXEL_SCALE, VOXEL_SCALE);
		rng = new RandomNumberGenerator();
		_material  = new StandardMaterial3D {
			AlbedoColor = new Color(rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f))
		};
        
		//_shaderfile = GD.Load<RDShaderFile>("res://chunkgen.glsl");
		//SetChunkPosition(new Vector2I(0,0));
	}

    public static int PackBlockInfo(int blockType, int x, int y, int z) {
        return blockType<<25 | z<<15 | y<<5 | x;
    }

    public struct BlockInfo {
        public int BlockType;
        public int X;
        public int Y;
        public int Z;
    }

    public static BlockInfo UnpackBlockInfo(int blockInfo) {
        return new BlockInfo {
            BlockType = blockInfo >> 15 & 0x3ff, // 10 bit bit mask
            Z = (blockInfo >> 10) & 0x1f, // 5 bit bit mask
            Y = (blockInfo >> 5) & 0x1f,
            X = blockInfo & 0x1f
        };
    }

    public static bool IsBlockEmpty(int blockInfo) {
        return (blockInfo >> 15 & 0x3ff) == 0;
    }

	public void Generate()
	{
		if (Engine.IsEditorHint()) return;

        for (int x=0;x<CHUNK_SIZE;x++) {
            for (int y=0;y<CHUNK_SIZE;y++) {
                for (int z=0;z<CHUNK_SIZE;z++) {
					//Block block;
					int block_idx = x + y * CHUNKSQ + z * CHUNK_SIZE;
                    if (block_idx >= _blocks.Length) continue;  

                    var globalBlockPosition = ChunkPosition * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z);                 
					var groundHeight = (int)(0.1f * CHUNK_SIZE + 4f*(Noise.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Y) + 1f));

                    int blockType;
                    if (y==0) blockType = BlockManager.BlockID("Lava");
                    else if (y<groundHeight/2) blockType = BlockManager.BlockID("Stone");
                    else if (y<groundHeight) blockType = BlockManager.BlockID("Dirt");
                    else if (y==groundHeight) blockType = BlockManager.BlockID("Grass");
					else blockType = BlockManager.BlockID("Air");
                   
                   int blockinfo = blockType<<15 | z<<10 | y<<5 | x;
                   _blocks[block_idx] = blockinfo;
                }
            }
        }
	}

	public void Update() {
        _arraymesh.ClearSurfaces();
		BuildChunkMesh(_blocks);
		CollisionShape.Shape = MeshInstance.Mesh.CreateTrimeshShape();
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

    private void BuildChunkMesh(int[] chunk) {
        // data is an array of dictionaries, one for each axis
        // each dictionary is a hash map of block types to a set binary planes
        // we need to group by block type like this so we can batch the meshing and texture blocks correctly
        Dictionary<int, Dictionary<int, UInt32[]>>[] data = new Dictionary<int,Dictionary<int, UInt32[]>>[6];
        for (short i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value

        var axis_cols = new UInt32[CSP3*3];
        var col_face_masks = new UInt32[CSP3*6];

        // generate binary 0 1 voxel representation for each axis
        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z++) {
                    var pos = new Vector3I(x,y,z)-Vector3I.One; 
                    // goofy ahh check for out of bounds
                    if (pos.X<0||pos.X>=CHUNK_SIZE||pos.Y<0||pos.Y>=CHUNK_SIZE||pos.Z<0||pos.Z>=CHUNK_SIZE) continue; 
                    var chunk_idx = pos.X + pos.Z*CHUNK_SIZE + pos.Y*CHUNKSQ;
                    
                    var b = chunk[chunk_idx];
                    if ((b >> 15 & 0x3ff) != 0) { // if block is solid
                        axis_cols[x + z*CSP] |= (UInt32)1 << y;           // y axis defined by x,z
                        axis_cols[z + y*CSP + CSP2] |= (UInt32)1 << x;    // x axis defined by z,y
                        axis_cols[x + y*CSP + CSP2*2] |= (UInt32)1 << z;  // z axis defined by x,y
                    }
                }
            }
        }

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
                        var blocktype = (_blocks[voxel_pos.X + voxel_pos.Z * CHUNK_SIZE + voxel_pos.Y * CHUNKSQ] >> 15) & 0x3ff;
                        if (!data[axis].TryGetValue(blocktype, out Dictionary<int, UInt32[]> planeSet)) {
                            planeSet = new(); 
                             data[axis].Add(blocktype, planeSet);
                        }
                        if (!planeSet.TryGetValue(k, out UInt32[] data_entry)) {
                            data_entry = new UInt32[CHUNK_SIZE];
                            planeSet.Add(k, data_entry);
                        }
                        data_entry[j] |= (UInt32)1 << i;     // push the "row" bit into the "column" UInt32
                        planeSet[k] = data_entry;
                    }
                }
            }
        }

        // construct mesh
        _st.Begin(Mesh.PrimitiveType.Triangles);
        _st2.Begin(Mesh.PrimitiveType.Triangles);
        for (int axis=0; axis<6;axis++) {
            foreach (var (blockType, planeSet) in data[axis]) {
                foreach (var (k, binary_plane) in planeSet) {
                    var greedy_quads = GreedyMeshBinaryPlane(binary_plane);
                    foreach (GreedyQuad quad in greedy_quads) {
                        Vector3I quad_offset, quad_delta; // row and col, width and height
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

                        // construct vertices and normals for mesh
                        Godot.Vector3[] verts = new Godot.Vector3[4];
                        for (int i=0; i<4; i++) {
                            verts[i] = quad_offset + (Godot.Vector3)CUBE_VERTS[AXIS[axis,i]]*quad_delta;
                        }
                        Godot.Vector3[] triangle1 = {verts[0], verts[1], verts[2]};
                        Godot.Vector3[] triangle2 = {verts[0], verts[2], verts[3]};
                        Godot.Vector3 normal = axis switch
                        {
                            0 => Godot.Vector3.Down,
                            1 => Godot.Vector3.Up,
                            2 => Godot.Vector3.Left,
                            3 => Godot.Vector3.Right,
                            4 => Godot.Vector3.Back, // -z is forward in godot
                            _ => Godot.Vector3.Forward
                        };
                        Godot.Vector3[] normals = {normal, normal, normal};
                        
                        // store the up down left and right textures in the colour channel
                        var block_face_texture_idx = BlockManager.BlockTextureArrayPositions(blockType)[axis];
                        // store the current axis and the texture index in the uv channel
                        var uv = new Godot.Vector2(axis, block_face_texture_idx);
                        Godot.Vector2[] uvs = {uv, uv, uv};

                        // add the quad to the mesh
                        if (blockType == lavablockid) {
                            _st2.AddTriangleFan(triangle1, normals: normals);
                            _st2.AddTriangleFan(triangle2, normals: normals);
                        }
                        else {
                            _st.AddTriangleFan(triangle1, uvs: uvs, normals: normals);
                            _st.AddTriangleFan(triangle2, uvs: uvs, normals: normals);
                        }
                    }
                }
            }
        }

        //_st.SetMaterial(BlockManager.Instance.ChunkMaterial);
        //_st2.SetMaterial(BlockManager.Instance.LavaShader);
        var a1 = _st.Commit().SurfaceGetArrays(0);
        var a2 = _st2.Commit().SurfaceGetArrays(0);
        _arraymesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, a1);
        _arraymesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, a2);
        _arraymesh.SurfaceSetMaterial(0, BlockManager.Instance.ChunkMaterial);
        _arraymesh.SurfaceSetMaterial(1, BlockManager.Instance.LavaShader);
        MeshInstance.Mesh = _arraymesh;
    }

    // greedy quad for a 32 x 32 binary plane (assuming data length is 32) // CHANGED THIS TO 64 CHUNK SIZE
    // each Uint32 in data[] is a row of 32 bits
    // offsets along this row represent columns
    static private List<GreedyQuad> GreedyMeshBinaryPlane(UInt32[] data) { // modify this so chunks are 30 and padded 1 on each side to 32
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

	public void DamageBlocks(List<(Vector3I, int)> blockDamages)
	{ // array of tuples with block global position as Item1 and damage as Item2
		foreach ((Vector3I,int) blockdamage in blockDamages)
		{
			if (blockdamage.Item1.X < 0 || blockdamage.Item1.X >= Dimensions.X) continue;
			if (blockdamage.Item1.Y < 0 || blockdamage.Item1.Y >= Dimensions.Y) continue;
			if (blockdamage.Item1.Z < 0 || blockdamage.Item1.Z >= Dimensions.Z) continue;
			_blockHealth[blockdamage.Item1.X, blockdamage.Item1.Y, blockdamage.Item1.Z] -= blockdamage.Item2;
			if (_blockHealth[blockdamage.Item1.X, blockdamage.Item1.Y, blockdamage.Item1.Z] <= 0)
			{
				_blockHealth[blockdamage.Item1.X, blockdamage.Item1.Y, blockdamage.Item1.Z] = 0;
				//_blocks[blockdamage.Item1.X, blockdamage.Item1.Y, blockdamage.Item1.Z] = BlockManager.Instance.Air;
				_blocks[blockdamage.Item1.X + blockdamage.Item1.Z * Dimensions.X + blockdamage.Item1.Y * Dimensions.X * Dimensions.Z] &= ~0x3ff<<15; // set block to air
			}
		}
		Update();
	}

	public void SetBlock(Vector3I blockPosition, Block block)
	{
        var idx = blockPosition.X + blockPosition.Z * CHUNK_SIZE + blockPosition.Y * CHUNKSQ;
		//_blocks[blockPosition.X, blockPosition.Y, blockPosition.Z] = block;
        if (block == new Block()) _blocks[idx] &= ~0x3ff<<15; // set block to air
        else _blocks[idx] = _blocks[idx] &= BlockManager.BlockID("Stone")<<15;
		Update();
	}

	public Block GetBlock(Vector3I blockPosition)
	{
		return new Block();//_blocks[blockPosition.X, blockPosition.Y, blockPosition.Z];
	}
}