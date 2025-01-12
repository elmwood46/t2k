using Godot;
using System;

public partial class FragmentsProcessing : Node3D
{
    [Export] public string SaveName { get; set; }

    private bool _started = false;

    private Godot.Collections.Array<MeshInstance3D> _meshlist = new();

    public override void _Ready() {
        
        foreach (Node child in GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                _meshlist.Add(mesh);
                GD.Print($"Found mesh {mesh}");
            }
        }

        GD.Print($"\nFound {_meshlist.Count} meshes");

        var i=0;
        foreach (MeshInstance3D mesh in _meshlist) {
            i++;
            var rb = new RigidBody3D
            {
                Mass = 20.0f,
                Position = mesh.Position
            };

            // doesnt collide when initially spawned in
            rb.SetCollisionLayerValue(1,false);
            rb.SetCollisionLayerValue(2,false);
            rb.SetCollisionMaskValue(1,false);
            rb.SetCollisionMaskValue(2,false);
      
            AddChild(rb);
            rb.Owner = this;
            
            var col = mesh.Mesh.CreateConvexShape();
            var shape = new CollisionShape3D
            {
                Shape = col,
                Position = mesh.Position
            };
            AddChild(shape);
            shape.Owner = this;
            GD.Print($"created {shape} shape and {mesh} mesh for rigidbody {rb}.");
            GD.Print($"mesh inside tree: {mesh.IsInsideTree()}");
            GD.Print($"shape inside tree: {shape.IsInsideTree()}");
            mesh.Reparent(rb);
            shape.Reparent(rb);
        }
        GD.Print($"created {i} rigid bodies");
    }

    public override void _Process(double delta)
    {
        if (_started) return;
        CallDeferred(nameof(MethodName.DeferredSave));
        _started = true;
    }

    public void DeferredSave() {
        var ret = new PackedScene();
        ret.Pack(this);
        ResourceSaver.Save(ret, $"res://props/{SaveName}.tscn");

        GD.Print($"Node was inside tree: {IsInsideTree()}");
        GetTree().Quit();
    }
}