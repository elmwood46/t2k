using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

public partial class GreedyMeshTest : StaticBody3D  
{

	[Export]
	public FastNoiseLite Noise { get; set; }

    [Export] public MeshInstance3D MeshInstance {get;set;}
    [Export] public CollisionShape3D CollisionShape {get;set;}

    public const int GRID_SIZE = 20;

    public Dictionary<(int,int), bool> cells = new();

    public HashSet<(int,int)> processed= new();

    RandomNumberGenerator rng = new();

    List<MeshInstance3D> meshes = new();

    // vertices of a cube
    static readonly Vector3I[] CUBE_VERTS = 
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
    static readonly int[,] AXIS = 
        {
            {0, 4, 5, 1}, // bottom
            {2, 3, 7, 6}, // top
            {6, 4, 0, 2}, // left
            {3, 1, 5, 7}, // right
            {2, 0, 1, 3}, // front
            {7, 5, 4, 6}  // back
        };

    public int[] squareidx = {0, 2, 3, 1};
    public Godot.Vector3[] squareverts = {
        new(0, 0, 0),
        new(1, 0, 0),
        new(0, 0, 1),
        new(1, 0, 1)
    };


    public const int CHUNK_SIZE = 30; // the chunk size is 30, padded chunk size is 32
    public const int CHUNKSQ = CHUNK_SIZE*CHUNK_SIZE;
    public const int CSP = CHUNK_SIZE+2;
    public const int CSP2 = CSP*CSP; // squared padded chunk size
    public const int CSP3 = CSP2*CSP; // cubed padded chunk size

    public override void _Ready()
    {
        
        GetViewport().DebugDraw=Viewport.DebugDrawEnum.Wireframe;
        rng.Randomize();
        //Regenerate();
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustReleased("Generate")) {
            Regenerate();
        }

        if (Input.IsActionJustReleased("ToggleWireframe")) {
            if (GetViewport().DebugDraw==Viewport.DebugDrawEnum.Wireframe) {
                RenderingServer.SetDebugGenerateWireframes(false);
                GetViewport().DebugDraw=Viewport.DebugDrawEnum.Disabled;
            } else {
                RenderingServer.SetDebugGenerateWireframes(true);
                GetViewport().DebugDraw=Viewport.DebugDrawEnum.Wireframe;
            }
        }
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

    private MeshInstance3D BuildChunkMesh(bool[] chunk) {
        Dictionary<short, UInt32[]>[] data = new Dictionary<short, UInt32[]>[6];
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

                        if (!data[axis].TryGetValue((short)k, out UInt32[] data_entry)) data_entry = new UInt32[CHUNK_SIZE];
                        else data[axis].Remove((short)k);
                        data_entry[j] |= (UInt32)1 << i;     // push the "row" bit into the "column" UInt32
                        data[axis].Add((short)k, data_entry);
                    }
                }
            }
        }

        SurfaceTool st = new();
        var material = new StandardMaterial3D {AlbedoColor = new Color(rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f))};
        st.SetMaterial(material);
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int axis=0; axis<6;axis++) {
            for (short k=0; k<CHUNK_SIZE; k++) {
                if (!data[axis].TryGetValue(k, out UInt32[] binary_plane)) continue;
                
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
                    st.AddTriangleFan(triangle1, normals: normals);
                    st.AddTriangleFan(triangle2, normals: normals);
                }
            }
        }
        return new MeshInstance3D{Mesh = st.Commit()};
    }

    // greedy quad for a 32 x 32 binary plane (assuming data length is 32)
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

    private bool[] GenChunk() { // generate a padded chunk for testing, just a simple heightmap. 1 is solid, 0 is air. there is no bitwise stuff involved
        var _block32b = new bool[CHUNKSQ*CHUNK_SIZE];
        for (int x=0;x<CHUNK_SIZE;x++) {
            for (int y=0;y<CHUNK_SIZE;y++) {
                for (int z=0;z<CHUNK_SIZE;z++) {
					//Block block;
					int block_idx = x + y * CHUNKSQ + z * CHUNK_SIZE;
                    if (block_idx >= _block32b.Length) continue;                   
					var groundHeight = (int)(0.1f * CHUNK_SIZE + 4f*(Noise.GetNoise2D(x, z) + 1f));

					if (y <= groundHeight) _block32b[block_idx] = true;
					else _block32b[block_idx] = false;
                }
            }
        }
        return _block32b;
    }

    private void Regenerate() {
        foreach (MeshInstance3D m in meshes) m.QueueFree();
        meshes.Clear();

        // generate voxel data
        var data = GenChunk();
        
        // build the mesh
        var mesh = BuildChunkMesh(data);
        meshes.Add(mesh);
        AddChild(mesh);
    }

    private void Regenerate_2d() {
        cells.Clear();
        processed.Clear();

        foreach (MeshInstance3D mesh in meshes) {
            mesh.QueueFree();
        }
        meshes.Clear();

        // randomly fill grid
        for (int i=0; i< GRID_SIZE; i++)
        {
                for (int j=0; j< GRID_SIZE; j++)
                {
                    cells.Add((i, j), rng.Randf() > 0.1f);
                }
        }

        for (int x=0;x<GRID_SIZE;x++) {
            for (int y=0;y<GRID_SIZE;y++) {
                
                // check this cell has been processed or is empty (skip)
                if (processed.Contains((x,y)) || cells[(x,y)] == false) continue;

                // initialize w and height then use simple loops to check for contiguous cells
                int w=1, h=1;
                while (
                    x+w < GRID_SIZE 
                    && cells[(x+w,y)] == true
                    && !processed.Contains((x+w,y))
                ) {w++;}
                bool expand = true;
                while (y + h < GRID_SIZE && expand) {
                    for (int i=x; i<x+w;i++) {
                        if (cells[(i,y+h)] == false || processed.Contains((i,y+h))) {
                            expand = false;
                            break;
                        }
                    }
                    if (expand) h++;
                }

                // mark cells as processed
                for (int dx=0; dx<w; dx++) {
                    for (int dy=0; dy<h; dy++) {
                        processed.Add((x+dx,y+dy));
                    }
                }

                // use surface tool to build a mesh for each greedy square
                SurfaceTool surfaceTool = new();
                surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
                var material = new StandardMaterial3D {AlbedoColor = new Color(rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f), rng.RandfRange(0.5f,1.0f))};
                surfaceTool.SetMaterial(material);
                Godot.Vector3 offset = new(x,0,y);
                Godot.Vector3 doffset = new(w,0,h);
                Godot.Vector3[] verts = new Godot.Vector3[4];
                for (int i=0; i<4; i++) {
                    verts[i] = offset + squareverts[squareidx[i]]*doffset;
                }
                Godot.Vector3[] triangle1 = {verts[0], verts[1], verts[2]};
                Godot.Vector3[] triangle2 = {verts[0], verts[2], verts[3]};
                Godot.Vector3[] normals = {Godot.Vector3.Up, Godot.Vector3.Up, Godot.Vector3.Up};
                surfaceTool.AddTriangleFan(triangle1, normals: normals);
                surfaceTool.AddTriangleFan(triangle2, normals: normals);
                var mesh = new MeshInstance3D{Mesh = surfaceTool.Commit()};
                AddChild(mesh);
                meshes.Add(mesh);
            }
        }
    }
}