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

	private RandomNumberGenerator rng;
	private SurfaceTool _st = new();
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
	private bool[] _blocks = new bool[CHUNKSQ*CHUNK_SIZE];
	public Vector2I ChunkPosition { get; private set; }

	[Export]
	public FastNoiseLite Noise { get; set; }

	[Export]
	public FastNoiseLite WallNoise { get; set; }

	public void SetChunkPosition(Vector2I position)
	{
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
		CallDeferred(Node3D.MethodName.SetGlobalPosition, new Godot.Vector3(VOXEL_SCALE* ChunkPosition.X * Dimensions.X, 0, VOXEL_SCALE * ChunkPosition.Y * Dimensions.Z));

		Generate();
		Update();
	}

	public override void _Ready() {
		Scale = new Godot.Vector3(VOXEL_SCALE, VOXEL_SCALE, VOXEL_SCALE);
		rng = new RandomNumberGenerator();
		_material  = new StandardMaterial3D {
			AlbedoColor = new Color(rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f))
		};
		//_shaderfile = GD.Load<RDShaderFile>("res://chunkgen.glsl");
		//SetChunkPosition(new Vector2I(0,0));
	}

	public void Generate()
	{
		if (Engine.IsEditorHint()) return;

        for (int x=0;x<CHUNK_SIZE;x++) {
            for (int y=0;y<CHUNK_SIZE;y++) {
                for (int z=0;z<CHUNK_SIZE;z++) {

					var globalBlockPosition = ChunkPosition * new Vector2I(Dimensions.X, Dimensions.Z) + new Vector2I(x, z);
					//Block block;
					int block_idx = x + y * CHUNKSQ + z * CHUNK_SIZE;
                    if (block_idx >= _blocks.Length) continue;                   
					var groundHeight = (int)(0.1f * CHUNK_SIZE + 4f*(Noise.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Y) + 1f));

					if (y <= groundHeight) _blocks[block_idx] = true;
					else _blocks[block_idx] = false;
                }
            }
        }
	}

	public void Update() {
		MeshInstance.Mesh = BuildChunkMesh(_blocks);
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

    private ArrayMesh BuildChunkMesh(bool[] chunk) {
        Dictionary<short, UInt64[]>[] data = new Dictionary<short, UInt64[]>[6];
        for (short i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value

        var axis_cols = new UInt64[CSP3*3];
        var col_face_masks = new UInt64[CSP3*6];

        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z++) {
                    var pos = new Vector3I(x,y,z)-Vector3I.One; 
                    // goofy ahh check for out of bounds
                    if (pos.X<0||pos.X>=CHUNK_SIZE||pos.Y<0||pos.Y>=CHUNK_SIZE||pos.Z<0||pos.Z>=CHUNK_SIZE) continue; 
                    var chunk_idx = pos.X + pos.Z*CHUNK_SIZE + pos.Y*CHUNKSQ;
                    
                    var b = chunk[chunk_idx];
                    if (b) { // if block is solid
                        axis_cols[x + z*CSP] |= (UInt64)1 << y;           // y axis defined by x,z
                        axis_cols[z + y*CSP + CSP2] |= (UInt64)1 << x;    // x axis defined by z,y
                        axis_cols[x + y*CSP + CSP2*2] |= (UInt64)1 << z;  // z axis defined by x,y
                    }
                }
            }
        }

        for (int axis = 0; axis < 3; axis++) {
            for (int i=0; i<CSP2; i++) {
                var col = axis_cols[i + axis*CSP2];
                // sample descending axis and set true when air meets solid
                col_face_masks[CSP2*axis*2 + i] = col & ~(col << 1);
                // sample ascending axis and set true when air meets solid
                col_face_masks[CSP2*(axis*2+1) + i] = col & ~(col >> 1);
            }
        }

        // generate binary face plane data for each axis
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
                    col &= ~((UInt64)1 << CHUNK_SIZE);

                    // now get y coord of faces (it's their bit location in the UInt64, so trailing zeroes can find it)
                    while (col != 0) {
                        var k = BitOperations.TrailingZeroCount(col);
                        // clear least significant (rightmost) set bit
                        col &= col-1;

                        if (!data[axis].TryGetValue((short)k, out UInt64[] data_entry)) data_entry = new UInt64[CHUNK_SIZE];
                        else data[axis].Remove((short)k);
                        data_entry[j] |= (UInt64)1 << i;     // push the "row" bit into the "column" UInt32
                        data[axis].Add((short)k, data_entry);
                    }
                }
            }
        }

        _st.SetMaterial(_material);
        _st.Begin(Mesh.PrimitiveType.Triangles);
        for (int axis=0; axis<6;axis++) {
            for (short k=0; k<CHUNK_SIZE; k++) {
                if (!data[axis].TryGetValue(k, out UInt64[] binary_plane)) continue;
                
                var greedy_quads = GreedyMeshBinaryPlane(binary_plane);
                foreach (GreedyQuad quad in greedy_quads) {
                    Vector3I block_offset, quad_delta; // row and col, width and height
                    block_offset = axis switch
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
                    Godot.Vector3[] verts = new Godot.Vector3[4];
                    for (int i=0; i<4; i++) {
                        verts[i] = block_offset + (Godot.Vector3)CUBE_VERTS[AXIS[axis,i]]*quad_delta;
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
                    // add the quad to the mesh
                    _st.AddTriangleFan(triangle1, normals: normals);
                    _st.AddTriangleFan(triangle2, normals: normals);
                }
            }
        }
        var arrayMesh = _st.Commit();
        arrayMesh.SurfaceSetMaterial(0, BlockManager.Instance.ChunkMaterial);
        return arrayMesh;
    }

    // greedy quad for a 32 x 32 binary plane (assuming data length is 32) // CHANGED THIS TO 64 CHUNK SIZE
    // each Uint32 in data[] is a row of 32 bits
    // offsets along this row represent columns
    static private List<GreedyQuad> GreedyMeshBinaryPlane(UInt64[] data) { // modify this so chunks are 30 and padded 1 on each side to 32
        List<GreedyQuad> greedy_quads = new();
        int data_length = data.Length;
        for (int j=0;j<data_length;j++) { // j selects a row from the data[j]
            var i = 0; // i  traverses the bits in current row j
            while (i < CHUNK_SIZE) {
                i += BitOperations.TrailingZeroCount(data[j] >> i);
                if (i>=CHUNK_SIZE) continue;
                var h = BitOperations.TrailingZeroCount(~(data[j] >> i)); // count trailing ones from i upwards
                UInt64 h_as_mask = 0; // create a mask of h bits
                for (int xx=0;xx<h;xx++) h_as_mask |= (UInt64)1 << xx;
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
				_blocks[blockdamage.Item1.X + blockdamage.Item1.Z * Dimensions.X + blockdamage.Item1.Y * Dimensions.X * Dimensions.Z] = false;
			}
		}
		Update();
	}

	public void SetBlock(Vector3I blockPosition, Block block)
	{
        var idx = blockPosition.X + blockPosition.Z * CHUNK_SIZE + blockPosition.Y * CHUNKSQ;
		//_blocks[blockPosition.X, blockPosition.Y, blockPosition.Z] = block;
        if (block == new Block()) _blocks[idx] = false; // TODO -  if block is air
        else _blocks[idx] = true;
		Update();
	}

	public Block GetBlock(Vector3I blockPosition)
	{
		return new Block();//_blocks[blockPosition.X, blockPosition.Y, blockPosition.Z];
	}
}