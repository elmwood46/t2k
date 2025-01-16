using Godot;
using System;

[Tool]
public partial class genmesh : Node3D
{
    public override void _Ready() {
            MeshFactory.SimpleGrass();
            GD.Print("SimpleGrass mesh created");
    }
}
