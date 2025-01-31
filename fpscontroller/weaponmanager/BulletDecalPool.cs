using Godot;
using System;
using System.Collections.Generic;

public partial class BulletDecalPool : Node
{
    private const int MAX_BULLET_DECALS = 1000;
    private static readonly List<Node3D> decalPool = new();
    private static readonly PackedScene decalScene = GD.Load<PackedScene>("res://fpscontroller/weaponmanager/bullet_decal.tscn");
    public static void SpawnBulletDecal(Vector3 globalPos, Vector3 normal, Node3D parent, Basis bulletBasis, Texture2D textureOverride = null)
    {
        Node3D decalInstance;

        // Reuse or create a new decal
        if (decalPool.Count >= MAX_BULLET_DECALS && IsInstanceValid(decalPool[0]))
        {
            decalInstance = decalPool[0];
            decalPool.RemoveAt(0);
            decalPool.Add(decalInstance);
            Reparent(decalInstance, parent);
        }
        else
        {
            decalInstance = (Node3D)decalScene.Instantiate();
            parent.AddChild(decalInstance);
            decalPool.Add(decalInstance);
        }

        // Clear invalid instances
        if (decalPool.Count > 0 && !IsInstanceValid(decalPool[0]))
        {
            decalPool.RemoveAt(0);
        }

        // Set the decal's transform and align to the surface
        decalInstance.GlobalTransform = new Transform3D(bulletBasis, globalPos) * 
                                        new Transform3D(Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad(90)), Vector3.Zero);
        decalInstance.GlobalBasis = new Basis(new Quaternion(decalInstance.GlobalBasis.Y, normal)) * decalInstance.GlobalBasis;

        // Activate the particle effects
        var particles = decalInstance.GetNode<GpuParticles3D>("GPUParticles3D");
        particles.Emitting = true;

        // Set texture override if provided
        if (textureOverride is not null && decalInstance is Decal decal) decal.TextureAlbedo = textureOverride;
    }

    private static void Reparent(Node node, Node newParent)
    {
        node.GetParent()?.RemoveChild(node);
        newParent.AddChild(node);
    }
}
