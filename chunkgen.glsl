#[compute]
#version 450

// the number of dispatched threads is proportional to chunk size because we run once per block
layout(local_size_x = 16, local_size_y = 64, local_size_z = 16) in;

// input block information
layout(set=0, binding = 0, std430) restrict buffer readonly VoxelBuffer {
    uint voxeldata[];
} voxels;

// buffer which will hold vertex output
layout(set=0, binding = 1, std430) restrict buffer VertexBuffer {
    float ver[];
} vertex_buffer;

// voxel constants
#define DIMX 16
#define DIMY 64
#define DIMZ 16
#define NOBLOCK 0

const ivec3 CUBE_VERTS[8] = 
    ivec3[8](
		ivec3(0, 0, 0),
		ivec3(1, 0, 0),
		ivec3(0, 1, 0),
		ivec3(1, 1, 0),
		ivec3(0, 0, 1),
		ivec3(1, 0, 1),
		ivec3(0, 1, 1),
		ivec3(1, 1, 1)
    );

const int TOP[4] = int[4](2, 3, 7, 6);
const int BOTTOM[4] = int[4](0, 4, 5, 1);
const int LEFT[4] = int[4](6, 4, 0, 2);
const int RIGHT[4] = int[4](3, 1, 5, 7);
const int FRONT[4] = int[4](2, 0, 1, 3);
const int BACK[4] = int[4](7, 5, 4, 6);

// check if a voxel is empty
bool check_empty(int x, int y, int z) {
    if (x < 0 || x >= DIMX) return true;
    if (y < 0 || y >= DIMY) return true;
    if (z < 0 || z >= DIMZ) return true;

    // we use zero or one to check for voxel, you will need to calculate voxel type here in future
    return voxels.voxeldata[x + z * DIMX + y * DIMX * DIMZ] == NOBLOCK;
}

void create_face_mesh(int[4] face, ivec3 block_pos) {
    // calculate vertex positions
    vec3 voxverts[4] = vec3[4](
        vec3(CUBE_VERTS[face[0]] + block_pos), //a 
        vec3(CUBE_VERTS[face[1]] + block_pos), //b
        vec3(CUBE_VERTS[face[2]] + block_pos), //c
        vec3(CUBE_VERTS[face[3]] + block_pos)  //d
    );

    // calculate normals
    vec3 normal = normalize(cross(voxverts[2] - voxverts[0], voxverts[1] - voxverts[0]));

    // add triangles
    // 2 triangles * 3 vertex * 3 floats per vertex is eighteen floats
    // add first triangle to vertex buffer a-b-c
    int faceoffset = 0;
    if (face == TOP) faceoffset = 0;
    else if (face == BOTTOM) faceoffset = 1;
    else if (face == LEFT) faceoffset = 2;
    else if (face == RIGHT) faceoffset = 3;
    else if (face == FRONT) faceoffset = 4;
    else if (face == BACK) faceoffset = 5;

    // 4 verts + 1 normals per face = 5 vec3 = 15 floats per face per block = set aside 90 floats per block, 15 floats per face
    int vertIdx = 90*(block_pos.x+ block_pos.z* DIMX + block_pos.y * DIMX * DIMZ)+faceoffset*15;
    
    // indexes 0-11 are vertex data, 12-14 are normals
    for (int i = 0; i < 4; i++) {
        vertex_buffer.ver[vertIdx+i*3]   =  voxverts[i].x;
        vertex_buffer.ver[vertIdx+i*3+1] =  voxverts[i].y;
        vertex_buffer.ver[vertIdx+i*3+2] =  voxverts[i].z;
    }

    vertex_buffer.ver[vertIdx+12] = normal.x;
    vertex_buffer.ver[vertIdx+13] = normal.y;
    vertex_buffer.ver[vertIdx+14] = normal.z;
}

void create_block_mesh(ivec3 block_pos) {
    int x = block_pos.x, y = block_pos.y, z = block_pos.z;

    // skip if block is empty
    if (voxels.voxeldata[x + z * DIMX + y * DIMX * DIMZ] == NOBLOCK) return;
    
    // create face mesh for each exposed face
    // note -z is forward in godot so we reverse the order of the front/back faces
    if (check_empty(x,y+1,z)) create_face_mesh(TOP, block_pos);
    if (check_empty(x,y-1,z)) create_face_mesh(BOTTOM, block_pos);
    if (check_empty(x-1,y,z)) create_face_mesh(LEFT, block_pos);
    if (check_empty(x+1,y,z)) create_face_mesh(RIGHT, block_pos);
    if (check_empty(x,y,z-1)) create_face_mesh(FRONT, block_pos); //forward is -z in godot
    if (check_empty(x,y,z+1)) create_face_mesh(BACK, block_pos);
}

void main() {
    int xpos = int(gl_GlobalInvocationID.x);
    int ypos = int(gl_GlobalInvocationID.y);
    int zpos = int(gl_GlobalInvocationID.z);

    // create mesh for each block
    create_block_mesh(ivec3(xpos, ypos, zpos));
}