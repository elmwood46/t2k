using Godot;
using System;
using System.Linq;

// tool for importing a fragmented obj and turning it into rigid bodies with collision shapes

[Tool]
public partial class ImportFragmentObj : EditorScenePostImport
{
    public override GodotObject _PostImport(Node scene)
    {
        // add collision shapes to the imported objects
        ResourceSaver.Save(Iterate(scene), $"res://props/breakable/{scene.Name}.tscn");
        return scene; // Remember to return the imported scene
    }

    public PackedScene Iterate(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                var rb = new RigidBody3D
                {
                    Mass = 20.0f,
                    Position = mesh.Position,
                    Freeze = true,
                    FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic
                };
                rb.SetCollisionLayerValue(1,false);
                rb.SetCollisionLayerValue(2,false);
                rb.SetCollisionMaskValue(1,false);
                rb.SetCollisionMaskValue(2,false);

                rb.Position -= Vector3.One*0.5f;
        
                node.AddChild(rb);
                rb.Owner = node;
                
                var col = mesh.Mesh.CreateConvexShape();
                var shape = new CollisionShape3D
                {
                    Shape = col,
                    Position = mesh.Position
                };
                node.AddChild(shape);
                shape.Owner = node;
                GD.Print($"created {shape} shape and {mesh} mesh for rigidbody {rb}.");
                GD.Print($"mesh inside tree: {mesh.IsInsideTree()}");
                GD.Print($"shape inside tree: {shape.IsInsideTree()}");
                mesh.Owner = node;
                mesh.Reparent(rb);
                shape.Reparent(rb);
            }
        }

        var ret = new PackedScene();
        ret.Pack((Node3D)node);
        return ret;
    }
}


