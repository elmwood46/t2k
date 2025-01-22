using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public partial class Chunk : StaticBody3D
{
	[Export] public CollisionShape3D CollisionShape { get; set; }

	[Export] public MeshInstance3D MeshInstance { get; set; }

    //[Export] public Grass[] GrassMultiMeshArray {get; set;}

    private static readonly PackedScene _block_break_particles = GD.Load<PackedScene>("res://effects/break_block.tscn");

    private static readonly PackedScene _rigid_break = GD.Load<PackedScene>("res://effects/rigid_break2.tscn");

    private readonly Area3D _chunk_area = new() {Position = new Vector3(ChunkManager.Dimensions.X,0,ChunkManager.Dimensions.Z)*0.5f};
    private readonly CollisionShape3D _chunk_bounding_box = new() {
            Shape = new BoxShape3D { Size = new Vector3(ChunkManager.Dimensions.X, ChunkManager.Dimensions.Y, ChunkManager.Dimensions.Z) }
        };

	// 3d int array for holding blocks
    // each 32bit int contains packed block info: block type (10 bits), z (5 bits), y (5 bits), x (5 bits) 
    // this leaves 7 bits to implement block health or AO
	public Vector3I ChunkPosition { get; private set; }

	[Export]
	public FastNoiseLite WallNoise { get; set; }


    #region init
	public override void _Ready() {
		Scale = new Vector3(ChunkManager.VOXEL_SCALE, ChunkManager.VOXEL_SCALE, ChunkManager.VOXEL_SCALE);
        _chunk_area.AddChild(_chunk_bounding_box);
        AddChild(_chunk_area);
	}

	public void InitChunk(Vector3I position)
	{
        ChunkManager.UpdateChunkBlockData(position);
        ChunkManager.UpdateChunkMeshData(position);
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
        var newpos = new Vector3(
            ChunkManager.VOXEL_SCALE * ChunkPosition.X * ChunkManager.Dimensions.X,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Y * ChunkManager.Dimensions.Y * ChunkManager.SUBCHUNKS,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Z * ChunkManager.Dimensions.Z);
		
        CallDeferred(Node3D.MethodName.SetGlobalPosition, newpos);

        // HACK expensive LOD grass is disabled
        //UpdateChunkGrass(new float[] {newpos.X,newpos.Y,newpos.Z});

        Update();
	}
    #endregion

    #region set chunk pos

	public void SetChunkPosition(Vector3I position, int[] blocks)
	{
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
        var newpos = new Vector3(
            ChunkManager.VOXEL_SCALE * ChunkPosition.X * ChunkManager.Dimensions.X,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Y * ChunkManager.Dimensions.Y * ChunkManager.SUBCHUNKS,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Z * ChunkManager.Dimensions.Z);
		
        CallDeferred(Node3D.MethodName.SetGlobalPosition, newpos);
        
        /*
        _blocks = blocks;
        _chunkMeshData = await Task.Run(()=>{return ChunkManager.BuildChunkMesh(_blocks,ChunkPosition.Y == 0);});

         HACK base density expensive grass lod is disabled
        Grass[] newgrass = GrassMultiMeshArray;
        foreach (Grass grass in newgrass) {
            if (_chunkMeshData.HasSurfaceOfType(Chunk.ChunkMeshData.GRASS_SURFACE)) {
                grass.TerrainMesh = _chunkMeshData.GetSurface(Chunk.ChunkMeshData.GRASS_SURFACE);
            } else grass.TerrainMesh = null;

            grass.Multimesh = await Task.Run(()=>{return Grass.GenMultiMesh(grass);});
        }
        
        GrassMultiMeshArray = newgrass;

        //_chunkMeshData = await Task.Run(()=>{return ChunkManager.BuildChunkMesh(_blocks,ChunkPosition.Y == 0);});
       

        var mesh = _chunkMeshData.UnifySurfaces();
        var collisionHull = await Task.Run(()=>{return mesh.CreateTrimeshShape();});
         */

        Update();
	}
    #endregion

    // HACK expensive LOD grass is disabled
    /*
    public void UpdateChunkGrass(float[] newChunkPosition = null) {
        // HACK base density
        for (int i=0; i< GrassMultiMeshArray.Length; i++) {
            var g = GrassMultiMeshArray[i];
            if (_chunkMeshData.HasSurfaceOfType(ChunkMeshData.GRASS_SURFACE)) {
                g.TerrainMesh = _chunkMeshData.GetSurface(ChunkMeshData.GRASS_SURFACE);
            } else g.TerrainMesh = null;
            g.ChunkPosition = (newChunkPosition != null) ? new Vector3(newChunkPosition[0], newChunkPosition[1], newChunkPosition[2]) : GlobalPosition;
            g.PlayerPosition = (Player.Instance != null) ? Player.Instance.GlobalPosition : Vector3.Zero;
            g.Rebuild();
        }
    }

    public override void _ExitTree() {
        foreach (var g in GrassMultiMeshArray) g.Multimesh = null;
    }*/

    #region update

	public void Update() {
        var meshdata = ChunkManager.GetChunkMeshData(ChunkPosition);
        MeshInstance.Mesh = meshdata.UnifySurfaces();
        CollisionShape.Shape = MeshInstance.Mesh.CreateTrimeshShape();

        CallDeferred(MethodName.UpdateRigidBodies);
	}

    public void UpdateRigidBodies() {
        foreach (Node3D child in _chunk_area.GetOverlappingBodies()) {
            if (child is RigidBody3D rb) {
                //GD.Print("updating rigid body ", rb);
                rb.MoveAndCollide(Vector3.Zero);
            }
        }
    }

    #endregion

/*
    // HACK expensive LOD grass disabled for now
    public override void _Process(double delta) {
        if (Player.Instance != null) {
            var sq_dist = Player.Instance.GlobalPosition.DistanceSquaredTo(GlobalPosition+Vector3.One*VOXEL_SCALE*ChunkManager.CHUNK_SIZE/2);
            var chunk_dist  = Mathf.RoundToInt(sq_dist/(ChunkManager.CHUNK_SIZE*ChunkManager.CHUNK_SIZE*VOXEL_SCALE*VOXEL_SCALE));
            foreach (var g in GrassMultiMeshArray) g.Visible = false;
            if (chunk_dist < GrassMultiMeshArray.Length) GrassMultiMeshArray[chunk_dist].Visible = true;
        }
    }*/

    #region block setters
    public void SetBlock(int blockIndex, int blockType) {
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        _blocks[blockIndex] = (_blocks[blockIndex] & ~0xffff) | blockType;
        ChunkManager.UpdateChunkBlockData(ChunkPosition, _blocks);
    }

    public void SetBlockDamageFlag(int blockIndex, BlockDamageType damtype) {
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        int damMask = damtype switch  {
            BlockDamageType.Physical => 1<<5,
            BlockDamageType.Fire => 1<<6,
            _ => 1<<7
        };
        _blocks[blockIndex] |= damMask<<21; // << 16 then << 5, first 5 bits of damage data are reserved for damage percentage
        ChunkManager.UpdateChunkBlockData(ChunkPosition, _blocks);
    }

    public void SetBlockToAir(int blockIndex) {
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        _blocks[blockIndex] = 0;
        ChunkManager.UpdateChunkBlockData(ChunkPosition, _blocks);
    }

    public void SetBlockDamagePercentage(int blockIndex, float percentage) {
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        _blocks[blockIndex] = (_blocks[blockIndex] & ~0x1f0000) | ((((_blocks[blockIndex]>>16) & ~0x1f) | Math.Clamp(Mathf.RoundToInt(percentage*31.0),0,31))<<16);
        ChunkManager.UpdateChunkBlockData(ChunkPosition, _blocks);
    }

    public void SetBlockDamageInteger(int blockIndex, int damage) {
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        _blocks[blockIndex] = (_blocks[blockIndex] & ~0x1f0000) | ((((_blocks[blockIndex]>>16) & ~0x1f) | Math.Clamp(damage,0,31))<<16);
        ChunkManager.UpdateChunkBlockData(ChunkPosition, _blocks);
    }

	public int GetBlockInfoFromPosition(Vector3I blockPosition)
	{
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        if (blockPosition.X + blockPosition.Z * ChunkManager.CHUNK_SIZE + blockPosition.Y * ChunkManager.CHUNKSQ >= _blocks.Length) return -1;
		return _blocks[blockPosition.X + blockPosition.Z * ChunkManager.CHUNK_SIZE + blockPosition.Y * ChunkManager.CHUNKSQ];
	}
    #endregion

    #region block damage
	public void DamageBlocks(List<(Vector3I, int)> blockDamages)
	{ 
        // output dictionary of block positions where particles need to be spawned (for destroyed blocks) and textures for spawned particles
        var particle_spawn_list = new Godot.Collections.Dictionary<Vector3I,int>();
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);

        // array of tuples with block global position as Item1 and damage as Item2
		foreach ((Vector3I,int) blockdamage in blockDamages)
		{
			if (blockdamage.Item1.X < 0 || blockdamage.Item1.X >= ChunkManager.Dimensions.X) continue;
			if (blockdamage.Item1.Y < 0 || blockdamage.Item1.Y >= ChunkManager.Dimensions.Y*ChunkManager.SUBCHUNKS) continue;
			if (blockdamage.Item1.Z < 0 || blockdamage.Item1.Z >= ChunkManager.Dimensions.Z) continue;
            
            var block_idx = blockdamage.Item1.X
                + blockdamage.Item1.Z * ChunkManager.Dimensions.X
                + blockdamage.Item1.Y * ChunkManager.Dimensions.X * ChunkManager.Dimensions.Z;

            var blockinfo = _blocks[block_idx];

            //GD.Print("checking if block empty: ", BlockManager.BlockName(blockid));

            if (ChunkManager.IsBlockInvincible(blockinfo)) continue; // dont damage air blocks or invincible blocks


            // increase block damage percentage
            var block_damaged = (float)ChunkManager.GetBlockDamageInteger(blockinfo);
            block_damaged += blockdamage.Item2*ChunkManager.GetBlockFragility(blockinfo);
            var dam_rounded = Mathf.RoundToInt(block_damaged);
            SetBlockDamageInteger(block_idx, dam_rounded);
            SetBlockDamageFlag(block_idx, BlockDamageType.Physical);

			if (dam_rounded >= 31)
			{
                particle_spawn_list[blockdamage.Item1] = _blocks[block_idx];              
                SetBlockToAir(block_idx);
            }
		}

        // update mesh and collision shape
        ChunkManager.UpdateChunkBlockData(ChunkPosition, _blocks);
        ChunkManager.UpdateChunkMeshData(ChunkPosition);


        // HACK expensive LOD grass is disabled
        //CallDeferred(nameof(Chunk.UpdateChunkGrass), null);

		Update();
        SpawnBlockParticles(particle_spawn_list);
	}



    public void SpawnBlockParticles(Godot.Collections.Dictionary<Vector3I, int> positionsAndBlockInfo) {
        if (positionsAndBlockInfo.Count == 0) return;
        // sort dictionary by distance to player
        /*
        var globalChunkPos = new Godot.Vector3 (ChunkPosition.X * Dimensions.X, ChunkPosition.Y * Dimensions.Y * SUBCHUNKS, ChunkPosition.Z*Dimensions.Z);  
        var sortedByMagnitude = positionsAndTextures.ToImmutableSortedDictionary(
            pos => pos.Key,
            tex => tex.Value,
            Comparer<Vector3I>.Create((a, b) => 
                (
                    Math.Sign(
                        ((globalChunkPos + new Godot.Vector3(a.X,a.Y,a.Z)) - Player.Instance.GlobalPosition).LengthSquared()
                        - ((globalChunkPos + new Godot.Vector3(b.X,b.Y,b.Z)) - Player.Instance.GlobalPosition).LengthSquared()
                    )
                )
            )
        );*/

        // spawn particles from closest to fartherest from player
        // the particles spawned first have more detail and more expensive collisions\
        var _blocks = ChunkManager.GetChunkBlockData(ChunkPosition);
        var blocks_being_destroyed = positionsAndBlockInfo.Count;
        var blockCount = 0;
        var partcount = GetTree().GetNodesInGroup("RigidBreak").Count;
        foreach (var (pos, block_info) in positionsAndBlockInfo) {
            var is_block_above = false;
            var block_idx = Mathf.FloorToInt(pos.X)
            + Mathf.FloorToInt(pos.Z) * ChunkManager.CHUNK_SIZE
            + Mathf.FloorToInt(pos.Y) * ChunkManager.CHUNKSQ;
            var block_above_idx = block_idx + ChunkManager.CHUNKSQ;
            if (block_above_idx <=_blocks.Length) {
                var block_above_id = ChunkManager.GetBlockID(_blocks[block_above_idx]);
                if (block_above_id != 0) is_block_above = true;
            }
            //if (is_block_above) GD.Print("block above");
        
            var particles = _rigid_break.Instantiate() as RigidBreak;

            // optimize particles to avoid framerate drop
            // for 4x4 fragments
            /*
            particles.MaskHalves = partcount > 4 || blockCount > 3;
            particles.HalfStrength = partcount > 8  || blockCount > 6;               
            particles.QuarterStrength = partcount > 10 || blockCount > 8;
            particles.EighthStrength = partcount > 12 || blockCount > 9;
            particles.OnlyOneParticle = partcount > 14 || blockCount > 11;
            */

            // optimize particles to avoid framerate drop     
            // for 3x3 fragments
            /*
            if (partcount > 10 || blockCount > 5)
                particles.DecayTime = Mathf.Max(0.5f, 3.0f - (partcount + blockCount) * 0.3f);
            particles.MaskHalves = partcount > 12 || blockCount > 10;
            particles.HalfStrength = partcount > 15  || blockCount > 13;
            particles.QuarterStrength = partcount > 20 || blockCount > 16;*/
            /*
            particles.MaskHalves = partcount > 8 || blockCount > 9;
            if (particles.MaskHalves) particles.DecayTime = 2.0f;
            particles.HalfStrength = partcount > 12  || blockCount > 12;
            if (particles.HalfStrength) particles.DecayTime = 0.5f;
            particles.QuarterStrength = partcount > 15 || blockCount > 14;
            if (particles.QuarterStrength) particles.DecayTime = 0.25f;
            particles.EighthStrength = partcount > 16 || blockCount > 15;
            //particles.OnlyOneParticle = partcount > 17 || blockCount > 16;*/


            // optimize particles to avoid framerate drop         
            if (blocks_being_destroyed > 1) { // destroying more than one block at once
                if (partcount + blockCount < 1) particles.BlockDivisions = 3;
            } else {
                if (partcount + blockCount < 15) particles.BlockDivisions = 3;
                if (partcount + blockCount < 5) particles.BlockDivisions = 4;
            }

            var mult = 3.0f;
            if (partcount > 10*mult || blockCount > 5*mult)
                particles.DecayTime = Mathf.Max(0.5f, 2.0f - partcount * 0.1f);
            particles.HalfStrength = partcount > 5*mult  || blockCount > 5*mult;        
            particles.MaskHalves = partcount >= 8 || blockCount > 6*mult;       
            particles.QuarterStrength = partcount > 10*mult || blockCount > 8*mult;
            particles.EighthStrength = partcount > 12*mult || blockCount > 9*mult;
            particles.OnlyOneParticle = partcount > 14*mult || blockCount > 11*mult;

            // set particles position
            // add 1/BLockDivisions to center the particles in the grid (since pos is floored block position)
            // and subtract a small amount to avoid z-fighting with top particles as well as keep them in their block
            particles.Position = pos + Vector3.One*(1/particles.BlockDivisions) - Vector3.Up*0.0625f; 


            if (is_block_above) particles.NoUpwardsImpulse = true;

            particles.BlockInfo = block_info;

            
            
            AddChild(particles);
            var impulse_pos = (particles.GlobalTransform.Origin - Player.Instance.GlobalTransform.Origin).Normalized() * 100.0f; 
            particles.StartingImpulse = impulse_pos;
            blockCount++;
            //partcount++;
            
            /*
            // HPU PARTICLES INSTEAD OF RIGID BODIES
            var particles = _block_break_particles.Instantiate() as GpuParticles3D;
            if (blockCount > 5) {
                //particles.Amount = Math.Max(1, 256 - blockCount * 10); // spawn less particles per block, the more blocks you break
                //GD.Print("spawned ", particles.Amount, " particles");
            }
            var partmat =  particles.DrawPass1.SurfaceGetMaterial(0) as StandardMaterial3D;
            partmat.AlbedoTexture = tex;
            partmat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            particles.DrawPass1.SurfaceSetMaterial(0, partmat);
            particles.Position = pos + Godot.Vector3.One*0.5f; // add 0.5 to center the particles in the grid
            AddChild(particles);
            var t = new Timer {WaitTime = particles.Lifetime};
            t.Timeout += () => {particles.QueueFree(); t.QueueFree();};
            AddChild(t);
            t.Start();
            particles.Emitting = true;*/
        }

    }
    #endregion

}