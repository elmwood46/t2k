using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ComputeChunk : Node
{
    /*
    public static readonly RDShaderFile ShaderFile = ResourceLoader.Load<RDShaderFile>("res://chunkgen.glsl"); 

    [Export] public bool DebugLog = false;

    [ExportCategory("Chunk World Size")]
    [Export(PropertyHint.Range, "0,32")] public int RenderDistance = 16;
    [Export(PropertyHint.Range, "0,16")] public int YChunks = 12;

    // GPU Variables
    private RenderingDevice _rd;
    private Rid _compute_shader;
    private Rid _compute_pipeline;
    private Godot.Collections.Array<RDUniform> _bindings;
    private Rid _uniform_set;
    private Rid _params_buffer;
    private RDUniform _params_uniform;
    private Rid _boid_data_buffer;

    private bool _skipped_first_physics_step = false;

    public override void _Ready()
    {
        // DEBUG set mouse mode enum
        Input.MouseMode = Input.MouseModeEnum.Captured;
        
        RenderingServer.CallOnRenderThread(Callable.From(() => SetupComputeShader()));
        RenderingServer.CallOnRenderThread(Callable.From(() => UpdateBoidsGpu(0.0f)));
    }

    private void UpdateBoidsGpu(float delta)
    {
        var paramsBufferBytes = GenerateParameterBuffer(delta);
        _rd.BufferUpdate(_params_buffer, 0, (uint)paramsBufferBytes.Length, paramsBufferBytes);
        RunComputeShader(_compute_pipeline);
    }

    private void RunComputeShader(Rid pipeline)
    {
        var computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, pipeline);
        _rd.ComputeListBindUniformSet(computeList, _uniform_set, 0);
        _rd.ComputeListDispatch(computeList, (uint)Math.Ceiling(NumBoids / 1024.0), 1, 1);
        _rd.ComputeListEnd();
    }

    private void UpdateDataTexture()
    {
        var boid_data_image_buffer = _rd.TextureGetData(_boid_data_buffer, 0);
        _boid_data.SetData(_image_size.X, _image_size.Y, false, Image.Format.Rgbah, boid_data_image_buffer);

        _boid_data_texture.Update(_boid_data);
    }

    private void SetupComputeShader()
    {
        _rd = RenderingServer.GetRenderingDevice();
        var shaderSpirv = ShaderFile.GetSpirV();
        _compute_shader = _rd.ShaderCreateFromSpirV(shaderSpirv);
        _compute_pipeline = _rd.ComputePipelineCreate(_compute_shader);

        var _num_blocks = ChunkManager.CSP3 * RenderDistance * RenderDistance * YChunks;
        var _chunk_data_buffer_rid = GenerateIntBuffer(_num_blocks);
        var _vertices_output_buffer_rid = GenerateFloatBuffer(_num_blocks * 3);
        var _vertices_output_buffer_rid = GenerateFloatBuffer(_num_blocks * 3);

        _boid_pos_buffer = GenerateVec3FloatBuffer(_boid_pos.ToArray());
        var boidPosUniform = GenerateUniform(_boid_pos_buffer, RenderingDevice.UniformType.StorageBuffer, 0);

        _boid_vel_buffer = GenerateVec3FloatBuffer(_boid_vel.ToArray());
        var boidVelUniform = GenerateUniform(_boid_vel_buffer, RenderingDevice.UniformType.StorageBuffer, 1);

        var paramsBufferBytes = GenerateParameterBuffer(0);
        _params_buffer = _rd.StorageBufferCreate((uint)paramsBufferBytes.Length, paramsBufferBytes);
        _params_uniform = GenerateUniform(_params_buffer, RenderingDevice.UniformType.StorageBuffer, 2);

        var fmt = new RDTextureFormat
        {
            Width = (uint)_image_size.X,
            Height = (uint)_image_size.Y,
            Format = RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit
        };

        var view = new RDTextureView();
        _boid_data_buffer = _rd.TextureCreate(fmt, view, new Godot.Collections.Array<byte[]>{_boid_data.GetData()});
        _boid_data_texture_rd = new()
        {
            TextureRdRid = _boid_data_buffer
        };
        var boidDataBufferUniform = GenerateUniform(_boid_data_buffer, RenderingDevice.UniformType.Image, 3);

        _bindings = new Godot.Collections.Array<RDUniform> { boidPosUniform, boidVelUniform, _params_uniform, boidDataBufferUniform };
        _uniform_set = _rd.UniformSetCreate(_bindings, _compute_shader, 0);
    }

    private Rid GenerateVec3FloatBuffer(Vector3[] data)
    {
        var dataBufferBytes = new byte[data.Length * 3 * sizeof(float)];
        var floats = ConvertVector3ArrayToFloatArray(data);
        Buffer.BlockCopy(floats, 0, dataBufferBytes, 0, dataBufferBytes.Length);
        return _rd.StorageBufferCreate((uint)dataBufferBytes.Length, dataBufferBytes);
    }

    private static float[] ConvertVector3ArrayToFloatArray(Vector3[] vectors)
    {
        float[] floatArray = new float[vectors.Length * 3]; // Each Vector3 has 3 floats

        for (int i = 0; i < vectors.Length; i++)
        {
            floatArray[i * 3] = vectors[i].X;
            floatArray[i * 3 + 1] = vectors[i].Y;
            floatArray[i * 3 + 2] = vectors[i].Z;
        }

        return floatArray;
    }

    private Rid GenerateIntBuffer(int size)
    {
        var dataBufferBytes = new byte[size * sizeof(int)];
        return _rd.StorageBufferCreate((uint)dataBufferBytes.Length, dataBufferBytes);
    }

    private Rid GenerateFloatBuffer(int size)
    {
        var dataBufferBytes = new byte[size * sizeof(float)];
        return _rd.StorageBufferCreate((uint)dataBufferBytes.Length, dataBufferBytes);
    }

    private static RDUniform GenerateUniform(Rid dataBuffer, RenderingDevice.UniformType type, int binding)
    {
        var dataUniform = new RDUniform
        {
            UniformType = type,
            Binding = binding
        };
        dataUniform.AddId(dataBuffer);
        return dataUniform;
    }

    private byte[] GenerateParameterBuffer(float delta)
    {
        var float_arr = new float[]
        {
            RenderDistance,
            WorldHeight
        };
        var dataBufferBytes = new byte[float_arr.Length * sizeof(float)];
        Buffer.BlockCopy(float_arr, 0, dataBufferBytes, 0, dataBufferBytes.Length);
        return dataBufferBytes;
    }

    private void FreeRids()
    {
        _rd.Sync();
        _rd.FreeRid(_uniform_set);
        _rd.FreeRid(_boid_data_buffer);
        _rd.FreeRid(_params_buffer);
        _rd.FreeRid(_boid_pos_buffer);
        _rd.FreeRid(_boid_vel_buffer);
        _rd.FreeRid(_compute_pipeline);
        _rd.FreeRid(_compute_shader);
        _rd.Free();
    }
    
    #region physics process and exit tree
    public override void _ExitTree()
    {
        RenderingServer.CallOnRenderThread(new Callable(this,MethodName.FreeRids));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_skipped_first_physics_step) _rd.Sync();
        else _skipped_first_physics_step = true;

        GetWindow().Title = $"Boids: {NumBoids}, FPS: {Engine.GetFramesPerSecond()}";
		RenderingServer.CallOnRenderThread(Callable.From(() => UpdateBoidsGpu((float)delta)));
        UpdateDataTexture();

        // DEBUG move camera
		var inputDirection = Input.GetVector("Left", "Right", "Back", "Forward").Normalized();
		var direction = ((Input.IsActionPressed("Jump") ? 1.0f : 0.0f) - (Input.IsActionPressed("Crouch") ? 1.0f : 0.0f)) * Vector3.Up;
		direction += new Vector3(inputDirection.X, 0.0f, -inputDirection.Y);
        direction = direction.Normalized();
		TestCameraContainer.GlobalPosition += TestCamera.GlobalBasis * direction * _movespeed;
    }
    #endregion
    */
}
