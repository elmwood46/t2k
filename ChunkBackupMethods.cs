using Godot;
using System;
using System.Collections.Generic;

/*
public partial class Chunk : StaticBody3D
{

	public void UpdateWithShaderCode()
	{
		var rd = RenderingServer.CreateLocalRenderingDevice();

		var shaderFile = GD.Load<RDShaderFile>("res://chunkgen.glsl");
		var shaderBytecode = shaderFile.GetSpirV();
		var shader = rd.ShaderCreateFromSpirV(shaderBytecode);

		// Create storage Buffers for Vertices
		// make it maximum size for now
		// this is 90 times the chunk dimensions.
		// 90 because 6 faces * 5 vectors (4 vertices + 1 normal) * 3 floats = 6*15 floats per block
		// we are setting aside 90 slots for each block in the chunk
		int vertexFloatCount = 90*Dimensions.X*Dimensions.Y*Dimensions.Z;
		// create empty buffer for vertices
		// initialize verts to -1
		// as they will always be positive when set by the shader, this lets us detect unset verts
		var vertbytes = new byte[vertexFloatCount * sizeof(float)];
		var vertarr = new float[vertexFloatCount];
		Array.Fill(vertarr,-1f);
		Buffer.BlockCopy(vertarr, 0, vertbytes, 0, vertbytes.Length);

		// copy voxels into the readonly voxbytes buffer
		var voxbytes = new byte[_block32b.Length * sizeof(int)];
		Buffer.BlockCopy(_block32b, 0, voxbytes, 0, voxbytes.Length);

		var voxelBuffer = rd.StorageBufferCreate((uint)voxbytes.Length, voxbytes);
		var vertexBuffer = rd.StorageBufferCreate((uint)vertbytes.Length, vertbytes);

		// Step 4: Bind Buffers to Uniforms
		var uniforms = new Godot.Collections.Array<RDUniform>
		{
			new() {UniformType = RenderingDevice.UniformType.StorageBuffer,Binding = 0},
			new() {UniformType = RenderingDevice.UniformType.StorageBuffer,Binding = 1},

		};
		uniforms[0].AddId(voxelBuffer);
		uniforms[1].AddId(vertexBuffer);

		var uniformSet = rd.UniformSetCreate(uniforms, shader, 0);

		var pipeline = rd.ComputePipelineCreate(shader);
		var computeList = rd.ComputeListBegin();
		rd.ComputeListBindComputePipeline(computeList, pipeline);
		rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
		rd.ComputeListDispatch(computeList, xGroups: 1, yGroups: 1, zGroups: 1); // 16 workgroups, but 4x16x4 threads each
		rd.ComputeListEnd();
		rd.Submit();

		// read back data after a short delay (~3 frames at 60fps) to give the GPU time to process
		var t = new Timer{WaitTime = 0.048,OneShot=true};
		t.Timeout += () => {
			rd.Sync();
			var vertexData = rd.BufferGetData(vertexBuffer);
			UpdateChunkMesh(vertexData);		
			t.QueueFree();
		};
		AddChild(t);
		t.Start();
		GD.Print("updated chunk ", ChunkPosition);
	}

    	private void UpdateChunkMesh(byte[] vertexData)
	{
		var vertexFloatCount = vertexData.Length / sizeof(float);
		var vertex_floats = new float[vertexFloatCount];
		Buffer.BlockCopy(vertexData, 0, vertex_floats, 0, vertexData.Length);
		
		_regularSurfaceTool.Begin(Mesh.PrimitiveType.Triangles);
		for (int block = 0; block < vertexFloatCount; block+=90) // 90 floats set aside for each block in the chunk
		{
			for (int face=0; face<90; face+=15) { // 15 floats set aside for each face, in the order top bottom left right forward back
				if (vertex_floats[block+face] == -1f) continue; // skip faces which never had vertices set
				Vector3 normal = new(vertex_floats[block+face+12], vertex_floats[block+face+13], vertex_floats[block+face+14]);
				Vector3[] normals = {normal, normal, normal};
				Vector3[] verts = new Vector3[4];
				for (int i=0; i < 4; i++) verts[i] = new(vertex_floats[block+face+i*3], vertex_floats[block+face+i*3+1], vertex_floats[block+face+i*3+2]); 
				
				Vector3[] triangle1 = {verts[0], verts[1], verts[2]};
				Vector3[] triangle2 = {verts[0], verts[2], verts[3]};

				_regularSurfaceTool.AddTriangleFan(triangle1, normals: normals);
				_regularSurfaceTool.AddTriangleFan(triangle2, normals: normals);
			}
		}

		_regularSurfaceTool.SetMaterial(new StandardMaterial3D());

		MeshInstance.Mesh = _regularSurfaceTool.Commit();
		CollisionShape.Shape = MeshInstance.Mesh.CreateTrimeshShape();

		GD.Print("generated mesh for chunk ", ChunkPosition);
	}
}
*/