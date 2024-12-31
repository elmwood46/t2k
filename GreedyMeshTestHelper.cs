using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
public partial class GreedyMeshTest : StaticBody3D {
    private MeshInstance3D BuildChunkMeshDemo(bool[] chunk) {
        // 6 hash maps, 2 for each axis (down and up, right and left, front (-z) and back(+z)) 
        // the short key is the axis offset (how deep we are in the chunk in the axis direction)
        // the value is a UInt32 array which represents the binary rows and columns of that plane of the chunk. 1 indicates a face, 0 is air
        // data is the array of such dictionaries
        Dictionary<short, UInt32[]>[] data = new Dictionary<short, UInt32[]>[6];
        for (short i=0; i<6; i++) data[i] = new(); // initialize the hash maps for each axis value

        // 3 planes of 64 bits for each voxel. each uint64 is one column, so one plane of 64 bits is UInt64*CSP
        // it's a volume, we do it for each axis so (1UInt64*CSP)^3 is where get the size CSP3*3
        var axis_cols = new UInt64[CSP3*3]; 
        // the col face masks are the descending and ascending faces for each axis, so we need double the size
        // it stores the information alternating, descending and ascending
        // descending y axis is "down" ascending y axis is "up" and so on
        var col_face_masks = new UInt64[CSP3*6]; 

        // create a binary grid plane for each axis, CSP bits wide and CSP bits deep
        // an O(N^3) algorithm
        for (int x=0;x<CSP;x++) {
            for (int y=0;y<CSP;y++) {
                for (int z=0;z<CSP;z++) {
                    var pos = new Vector3I(x,y,z)-Vector3I.One; // because chunk size is padded, we subtract one for the actual chunk block position
                    var chunk_idx = pos.X + pos.Z*CSP + pos.Y*CSP2 ;
                    if (chunk_idx < 0 || chunk_idx >= chunk.Length) continue; // skip if outside the chunk - equivalent to padding with nothing here
                    var b = chunk[chunk_idx];
                    if (b) { // if block is solid
                        axis_cols[x + z*CSP] |= (UInt64)1 << y;           // y axis defined by x,z
                        axis_cols[z + y*CSP + CSP2] |= (UInt64)1 << x;    // x axis defined by z,y
                        axis_cols[x + y*CSP + CSP2*2] |= (UInt64)1 << z;  // z axis defined by x,y

                        // for every solid voxel's X Y and Z faces, we make a binary representation and store in axis_cols
                        // this sets aside CSP2*3 indexes in axis_cols for each solid voxel
                        // representing the 3 planes of 64 bits for each axis
                        // we end up with CSP of such planes for each axis, because each axis is CSP deep
                        // thus the final size of the array is CSP3*3
                    }
                }
            }
        }

        // do face culling using the binary planes
        // col face masks is storing the descending and ascending faces for each axis,
        // alternating descending and then ascending 
        // e.g. xz (y axis) the descending masks are stored at indexes 0 to CSP2-1
        // and the ascending masks are stored at indexes CSP2 to 2*CSP2-1
        // then axis increments by one and we are on the z,y (x axis)
        // the descending masks are stored at indexes 2*CSP2 to 3*CSP2-1
        // and the ascending masks are stored at indexes 3*CSP2 to 4*CSP2-1
        // it increments by one and we are on the x,y (z axis)
        // the descending masks are stored at indexes 4*CSP2 to 5*CSP2-1
        // the ascending are stored at indexes 5*CSP2 to 6CSP2-1
        // we have stored 2 planes of 64 bit INTS for each axis, so we have 6 planes of 64 bit INTS in total
        // 3 axis * 2 planes * CSP2 = 6*CSP3 
        // by using bit shifts we can do the culling for a column in one operation, so we have have 3*O(N^2) algorithm 
        for (int axis = 0; axis < 3; axis++) {
            for (int i=0; i<CSP2; i++) {
                // finx col for y, x and z axis, as they were stored above, by incrementing in steps of CSP2
                // i goes from 0 to CSP2, covering the binary plane for the given axis
                var col = axis_cols[i + axis*CSP2];
                // sample descending axis and set true when air meets solid
                col_face_masks[CSP2*(axis*2+0) + i] = col & ~(col << 1);
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

                    // removes rightmost padded bit (it's outside the chunk)
                    var col = col_face_masks[col_idx] >> 1;
                    // remove leftmost padded bit (it's outside the chunk)
                    // note CHUNK_SIZE = CSP-2
                    // we have already bit shifted "col" to the right by 1
                    // so we need to remove the leftmost bit which is now located at CSP-2 instead of CSP-1
                    // we we need to shift our mask by CSP-2, that is, left shift by CHUNK_SIZE
                    col &= ~((UInt64)1 << CHUNK_SIZE);
                    //GD.Print("col = ", col); 

                    // now get y coord of faces (it's their bit location in the UInt64, so trailing zeroes can find it)
                    while (col != 0) {
                        var k = BitOperations.TrailingZeroCount(col);
                        // clear least significant (rightmost) set bit
                        col &= col-1;

                        // get voxel position based on axis
                        // we have i k and j position of face, but translate it into chunkspace
                        // note that i and j is going across the binary plane for a given axis while k is going down the axis
                        // axis 0 and 1 are down and up, so we use j and i for "x" and "z" respectivly
                        // this matches the original voxel data which stores thing is XZY order (x+Z*CHUNK_SIZE + k*CHUNK_SIZE*CHUNK_SIZE)
                        // it also matches how it was stored in col_face_masks, with xz->k first, then zy->x, then yx->z
                        /*
                        Vector3I voxelPos = axis switch
                        {
                            0 or 1 => new Vector3I((int)j, (int)k, (int)i), // down, up    (y axis)
                            2 or 3 => new Vector3I((int)k, (int)i, (int)j), // right, left (x axis)
                            _ => new Vector3I((int)j, (int)i, (int)k),      // back, front (z axis)
                        };

                        var b = chunk[voxelPos.X+ voxelPos.Z*CHUNK_SIZE + voxelPos.Y*CHUNKSQ]; // block position is XZY in the chunk
                        */

                        var data_entry = data[axis].GetValueOrDefault((short)k, new UInt32[CHUNK_SIZE]);
                        // get the hash map for [axis] and step through to binary plane k
                        data_entry[j] |= (UInt32)1 << i; // set row [j] and column [i] of binary plane [k] for axis [axis] to be 1
                        data[axis][(short)k] = data_entry;
                        //GD.Print("set data for axis ", axis, " k ", k, " i ", i, " j ", j);
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
                if (!data[axis].TryGetValue(k, out UInt32[] binary_plane))
                {
                    //GD.Print("no data for axis ", axis, " k ", k);
                    continue;
                }
                //GD.Print("DATA FOR AXIS ", axis, " K ", k);
                var greedy_quads = GreedyMeshBinaryPlane(binary_plane);
                foreach (GreedyQuad quad in greedy_quads) {
                    Vector3I block_offset, quad_delta; // row and col, width and height
                    block_offset = axis switch
                    {
                        // row, col -> axis
                        0 => new Vector3I((int)quad.col, (int)k, (int)quad.row), // down, up    (xz -> y axis)
                        1 => new Vector3I((int)quad.col, (int)k+1, (int)quad.row), 
                        2 => new Vector3I((int)k, (int)quad.row, (int)quad.col), // left, right (zy -> x axis)
                        3 => new Vector3I((int)k+1, (int)quad.row, (int)quad.col), 
                        4 => new Vector3I((int)quad.col, (int)quad.row, (int)k), // back, front (xy -> z axis)
                        _ => new Vector3I((int)quad.col, (int)quad.row, (int)k-1)  // remember -z is forward in godot
                    };
                    quad_delta = axis switch
                    {
                        // row, col -> axis
                        0 => new Vector3I(quad.delta_col, 0, quad.delta_row),  // down, up    (xz -> y axis)
                        1 => new Vector3I(quad.delta_col, 0, quad.delta_row),
                        2  => new Vector3I(0, quad.delta_row, quad.delta_col), // right, left (zy -> x axis)
                        3 => new Vector3I(0, quad.delta_row, quad.delta_col),
                        4 => new Vector3I(quad.delta_col, quad.delta_row, 0),  // back, front (xy -> z axis)
                        _ => new Vector3I(quad.delta_col, quad.delta_row, 0),
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

        GD.Print("generated mesh");
        return new MeshInstance3D{Mesh = st.Commit()};
    }
}