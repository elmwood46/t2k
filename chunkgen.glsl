#[compute]
#version 450

// the number of dispatched threads is proportional to chunk size because we run once per block
layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

// input block information
layout(set=0, binding = 0, std430) restrict buffer readonly VoxelBuffer {
    uint voxeldata[];
} voxels;

// buffer which will hold vertex output
layout(set=0, binding = 1, std430) restrict buffer VertexBuffer {
    float ver[];
} vertex_buffer;

// buffer to hold normals
layout(set=0, binding = 2, std430) restrict buffer NormalBuffer {
    float nrm[];
} normal_buffer;

// index buffer
layout(set=0, binding = 3, std430) restrict buffer IndexBuffer {
    int idx[];
} index_buffer;

// atomic counter for the number of indices
layout(set=0, binding = 4, std430) buffer AtomicIndexCounterBuffer {
    uint[] i;
} _idxcounter;
