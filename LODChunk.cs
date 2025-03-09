using Godot;
using System;

public partial class LODChunk : Node3D
{
    [Export] public MeshInstance3D MeshInstance;

    public Vector3I ChunkPosition { get; private set; } = Vector3I.MaxValue;

    public void SetChunkPosition(Vector3I position)
	{
        ChunkPosition = position;
        var newpos = new Vector3(
            ChunkManager.VOXEL_SCALE * ChunkPosition.X * ChunkManager.Dimensions.X,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Y * ChunkManager.Dimensions.Y * ChunkManager.SUBCHUNKS,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Z * ChunkManager.Dimensions.Z);
        //SetGlobalPosition(newpos);
    }

    public void UpdateMesh(Mesh mesh)
    {
        MeshInstance.Mesh = mesh;
    }
}