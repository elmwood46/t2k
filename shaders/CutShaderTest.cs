using Godot;
using System;

[Tool]
public partial class CutShaderTest : MeshInstance3D
{
    [Export] MeshInstance3D CutPlane {get; set;}

    public override void _Process(double delta) {
        if (CutPlane != null) {
            if (MaterialOverride is ShaderMaterial shader) {
                shader.SetShaderParameter("cutplane", CutPlane.Transform);
            }
            if (MaterialOverride.NextPass is ShaderMaterial shader2) {
                shader2.SetShaderParameter("cutplane", CutPlane.Transform);
            }
        }
    }
}
