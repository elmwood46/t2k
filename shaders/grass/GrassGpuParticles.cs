using System;
using Godot;

[Tool]
public partial class GrassGpuParticles : Node3D
{
    // Grass Particles Shader parameters
    [ExportCategory("Grass Particles Shader")]
    [Export] public GpuParticles3D GpuParticlesInstance { get; set; }

    [ExportGroup("Particles")]
    [Export] public float Width { get; set; } = 20f;
    [Export] public float Height { get; set; } = 20f;
    [Export] public int NumParticles { get; set; } = 10;
    [Export(PropertyHint.Range,"0.0,360.0,0.01")] public float WindAngle { get; set; } = 0f;
    [Export(PropertyHint.Range,"0.0,1.0,0.001")] public float WindSpeed { get; set; } = 0f;
    [Export(PropertyHint.Range,"0.0,1.0,0.001")] public float WindStrength { get; set; } = 0f;
    [Export] public Texture2D Noise { get; set; }
    [Export] public Texture2D NoiseColor { get; set; }

    // Spatial shader parameters
    [ExportGroup("Spatial Instance")]
    [Export(PropertyHint.ColorNoAlpha)] public Color Color1 { get; set; } = new Color(0.106f, 0.424f, 0.0f,1.0f);
    [Export(PropertyHint.ColorNoAlpha)] public Color Color2 { get; set; } = new Color(0.424f, 0.678f, 0.11f, 1.0f);

    [Export] public Texture2D ColorCurve { get; set; }

    public int NumGrassBlocks {get; set;} = 0;

    // size of this is the max number of grass blocks that can be rendered
    private int[] _grass_position_indices = new int[683];
    private float[] _grass_positions;

    public override void _Ready() {
        // 3 floats needed to store block positions
        _grass_positions = new float[_grass_position_indices.Length*3];

        if (GpuParticlesInstance.ProcessMaterial is ShaderMaterial shaderMaterial)
        {
            shaderMaterial.SetShaderParameter("num_particles", NumParticles);
            shaderMaterial.SetShaderParameter("wind_angle", WindAngle);
            shaderMaterial.SetShaderParameter("wind_speed", WindSpeed);
            shaderMaterial.SetShaderParameter("wind_strength", WindStrength);
            shaderMaterial.SetShaderParameter("_noise", Noise);
            shaderMaterial.SetShaderParameter("_noisecolor", NoiseColor);
        }
    }

    public override void _Process(double delta)
    {
        if (GpuParticlesInstance != null)
        {
            UpdateParameters();
        }
    }

    public void UpdateParameters()
    {
        if (GpuParticlesInstance.ProcessMaterial is ShaderMaterial shaderMaterial)
        {
            GpuParticlesInstance.Amount = NumParticles;

            if (NumGrassBlocks == 0) 
            {
                GpuParticlesInstance.Emitting = false;
                return;
            }
            else GpuParticlesInstance.Emitting = true;

            shaderMaterial.SetShaderParameter("num_grass_blocks", NumGrassBlocks);
            shaderMaterial.SetShaderParameter("grass_positions", _grass_positions);
            shaderMaterial.SetShaderParameter("grass_indices", _grass_position_indices);
        }

        // Spatial shader parameters
        if (GpuParticlesInstance.DrawPass1.SurfaceGetMaterial(0) is ShaderMaterial spatialShader)
        {
            spatialShader.SetShaderParameter("color1", Color1);
            spatialShader.SetShaderParameter("color2", Color2);
            spatialShader.SetShaderParameter("_colorcurve", ColorCurve);
        }
    }

    public void SetShaderGrassPositions(Vector3 chunkPosition, int[] blocks) {
            Array.Fill(_grass_positions, -1);
            Array.Fill(_grass_position_indices, -1);
            int grasscount = 0, skipped_blocks = 0;
            for (int i=0; i<blocks.Length; i++) {
                var y = i / ChunkManager.CHUNKSQ;
                if  (ChunkManager.GetBlockID(blocks[i]) == BlockManager.BlockID("Grass")
                        /*&&
                        (
                            y==ChunkManager.CHUNK_SIZE-1
                            ||
                            (
                                (i+ChunkManager.CHUNKSQ < blocks.Length) && ChunkManager.IsBlockEmpty(blocks[i+ChunkManager.CHUNKSQ])
                            )
                        )*/
                    )
                {
                    var x = i % ChunkManager.CHUNK_SIZE;
                    var z = (i / ChunkManager.CHUNK_SIZE) % ChunkManager.CHUNK_SIZE;

                    // put a vertex in the array
                    var idx = 3*(i-skipped_blocks);
                    _grass_positions[idx] = chunkPosition.X + (float)x;
                    _grass_positions[idx+1] = chunkPosition.Y + (float)y;
                    _grass_positions[idx+2] = chunkPosition.Z + (float)z;
                    _grass_position_indices[grasscount++]=idx;
                }
                else skipped_blocks++;

                if (grasscount >= _grass_position_indices.Length) break;
            }

            // update the grass particles
            NumGrassBlocks = grasscount;
            UpdateParameters();
    }
}
