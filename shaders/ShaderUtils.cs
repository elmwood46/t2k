// CC0 license: use this function wherever you want, no need to credit.
// Call this function on any Node3D to apply the weapon_clip_and_fov_shader.gdshader to all meshes within it.

using System.Collections.Generic;
using Godot;

public static class ShaderUtils
{
    public static void ApplyClipAndFovShaderToViewModel(Node3D node3D, float fovOrNegativeForUnchanged = -1.0f)
    {
        var allMeshInstances = node3D.GetChildrenRecursive<MeshInstance3D>();
        if (node3D is MeshInstance3D meshInstance3D)
        {
            allMeshInstances.Add(meshInstance3D);
        }

        foreach (var meshInstance in allMeshInstances)
        {
            var mesh = meshInstance.Mesh;
            // Turn shadow casting off for view model to avoid issues.
            meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

            for (int surfaceIdx = 0; surfaceIdx < mesh.GetSurfaceCount(); surfaceIdx++)
            {
                var baseMaterial = mesh.SurfaceGetMaterial(surfaceIdx) as BaseMaterial3D;
                if (baseMaterial == null) continue;

                var weaponShaderMaterial = new ShaderMaterial
                {
                    Shader = GD.Load<Shader>("res://shaders/WeaponClipAndFOV.gdshader")
                };

                weaponShaderMaterial.SetShaderParameter("texture_albedo", baseMaterial.AlbedoTexture);
                weaponShaderMaterial.SetShaderParameter("texture_metallic", baseMaterial.MetallicTexture);
                weaponShaderMaterial.SetShaderParameter("texture_roughness", baseMaterial.RoughnessTexture);
                weaponShaderMaterial.SetShaderParameter("texture_normal", baseMaterial.NormalTexture);

                weaponShaderMaterial.SetShaderParameter("albedo", baseMaterial.AlbedoColor);
                weaponShaderMaterial.SetShaderParameter("metallic", baseMaterial.Metallic);
                weaponShaderMaterial.SetShaderParameter("specular", baseMaterial.MetallicSpecular);
                weaponShaderMaterial.SetShaderParameter("roughness", baseMaterial.Roughness);
                weaponShaderMaterial.SetShaderParameter("viewmodel_fov", fovOrNegativeForUnchanged);

                var texChannels = new[]
                {
                    new Vector4(1, 0, 0, 0),
                    new Vector4(0, 1, 0, 0),
                    new Vector4(0, 0, 1, 0),
                    new Vector4(1, 0, 0, 1),
                    new Vector4(0, 0, 0, 0)
                };

                weaponShaderMaterial.SetShaderParameter("metallic_texture_channel", texChannels[(int)baseMaterial.MetallicTextureChannel]);
                mesh.SurfaceSetMaterial(surfaceIdx, weaponShaderMaterial);
            }
        }
    }

    private static List<T> GetChildrenRecursive<T>(this Node node) where T : class
    {
        var result = new List<T>();
        foreach (Node child in node.GetChildren())
        {
            if (child is T tChild)
            {
                result.Add(tChild);
            }

            if (child is Node childNode)
            {
                result.AddRange(childNode.GetChildrenRecursive<T>());
            }
        }

        return result;
    }
}
