using Godot;
using System;
using System.Collections.Generic;

[Tool]
[GlobalClass]
public partial class DestructibleMeshData : Resource {
    public readonly float Health;
    public readonly float MaxHealth;
    public readonly PackedScene IntactPacked;
    public readonly PackedScene BrokenPacked;
    public Transform3D IntactTransform;
    public Transform3D BrokenTransform;
    public DestructibleMeshType Type;
    public int PackedBlockDamageInfo;
    public int isChestOpened = -1; // -1 means no chest, 0 means chest and closed, 1 means chest and opened
    public List<Transform3D> ChestIntactLocalTransforms; // only set if isChestOpened >= 0

    public DestructibleMeshData(DestructibleMesh mesh)
    {
        Health = mesh.Health;
        MaxHealth = mesh.MaxHealth;
        IntactPacked = mesh.IntactPacked;
        BrokenPacked = mesh.BrokenPacked;
        // intact transform is global position scale and rotation
        IntactTransform = ((Node3D)mesh.IntactScene.GetChild(0)).GlobalTransform;
        BrokenTransform = mesh.BrokenScene.GlobalTransform;
        Type = mesh.Type;
        PackedBlockDamageInfo = mesh.PackedBlockDamageInfo;
        if (mesh is DestructibleChest c)
        {
            isChestOpened = c.IsOpened() ? 1 : 0;
            ChestIntactLocalTransforms = new List<Transform3D>();
            for (int i=0; i<6;i++)
            {
                var child = (Node3D)c.IntactScene.GetChild(0).GetChild(i);
                ChestIntactLocalTransforms.Add(child.Transform);
            }
        }
    }

    public DestructibleMeshData(DestructibleMeshData meshdata)
    {
        Health = meshdata.Health;
        MaxHealth = meshdata.MaxHealth;
        IntactPacked = meshdata.IntactPacked;
        BrokenPacked = meshdata.BrokenPacked;
        IntactTransform = meshdata.IntactTransform;
        BrokenTransform = meshdata.BrokenTransform;
        Type = meshdata.Type;
        PackedBlockDamageInfo = meshdata.PackedBlockDamageInfo;
        isChestOpened = meshdata.isChestOpened;
        ChestIntactLocalTransforms = meshdata.ChestIntactLocalTransforms;
    }
}