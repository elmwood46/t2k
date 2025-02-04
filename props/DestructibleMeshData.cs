using Godot;
using System;

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

    public DestructibleMeshData(DestructibleMesh mesh)
    {
        Health = mesh.Health;
        MaxHealth = mesh.MaxHealth;
        IntactPacked = mesh.IntactPacked;
        BrokenPacked = mesh.BrokenPacked;
        IntactTransform = ((Node3D)mesh.IntactScene.GetChild(0)).GlobalTransform;
        BrokenTransform = mesh.BrokenScene.GlobalTransform;
        Type = mesh.Type;
        PackedBlockDamageInfo = mesh.PackedBlockDamageInfo;
    }
}