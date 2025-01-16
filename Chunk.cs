using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public partial class Chunk : StaticBody3D
{
	[Export] public CollisionShape3D CollisionShape { get; set; }

	[Export] public MeshInstance3D MeshInstance { get; set; }
	public const float VOXEL_SCALE = 0.5f; // chunk space is integer based, so this is the scale of each voxel (and the chunk) in world space
    public const float INV_VOXEL_SCALE = 1/VOXEL_SCALE;

    // chunk size is 30, padded chunk size is 32. Can't be increased easily because it uses binary UINT32 to do face culling
	public const int CHUNK_SIZE = 30; // the chunk size is 62, padded chunk size is 64, // must match size in compute shader
    public const int CHUNKSQ = CHUNK_SIZE*CHUNK_SIZE;
    public const int CSP = CHUNK_SIZE+2;
    public const int CSP2 = CSP*CSP; // squared padded chunk size
    public const int CSP3 = CSP2*CSP; // cubed padded chunk size
	public static readonly Vector3I Dimensions = new(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);

    // in the Generate() method, noise >= this value doesnt generate blocks
    public const float NOISE_CUTOFF = 0.2f;

    public const int SUBCHUNKS = 1; // each one is an extra 32x32 chunk in the vertical y direction


    private static readonly PackedScene _block_break_particles = GD.Load<PackedScene>("res://effects/break_block.tscn");

    private static readonly PackedScene _rigid_break = GD.Load<PackedScene>("res://effects/rigid_break2.tscn");

    private readonly Area3D _chunk_area = new() {Position = new Godot.Vector3(Dimensions.X,0,Dimensions.Z)*0.5f};
    private readonly CollisionShape3D _chunk_bounding_box = new() {
            Shape = new BoxShape3D { Size = new Godot.Vector3(Dimensions.X, Dimensions.Y, Dimensions.Z) }
        };

	// 3d int array for holding blocks
    // each 32bit int contains packed block info: block type (10 bits), z (5 bits), y (5 bits), x (5 bits) 
    // this leaves 7 bits to implement block health or AO
	private int[] _blocks = new int[CHUNKSQ*CHUNK_SIZE*SUBCHUNKS];
	public Vector3I ChunkPosition { get; private set; }

	[Export]
	public FastNoiseLite WallNoise { get; set; }

	public void InitChunk(Vector3I position)
	{
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
		CallDeferred(Node3D.MethodName.SetGlobalPosition, new Godot.Vector3(
            VOXEL_SCALE * ChunkPosition.X * Dimensions.X,
            VOXEL_SCALE * ChunkPosition.Y * Dimensions.Y * SUBCHUNKS,
            VOXEL_SCALE * ChunkPosition.Z * Dimensions.Z)
        );

        _blocks = ChunkManager.Generate(ChunkPosition);
        var mesh = ChunkManager.BuildChunkMesh(_blocks, ChunkPosition.Y == 0);
        var collisionHull = mesh.CreateTrimeshShape();
        Update(mesh, collisionHull);
	}

	public async void SetChunkPosition(Vector3I position, int[] blocks)
	{
		ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
		CallDeferred(Node3D.MethodName.SetGlobalPosition, new Godot.Vector3(
            VOXEL_SCALE * ChunkPosition.X * Dimensions.X,
            VOXEL_SCALE * ChunkPosition.Y * Dimensions.Y * SUBCHUNKS,
            VOXEL_SCALE * ChunkPosition.Z * Dimensions.Z)
        );

        _blocks = blocks;
        var mesh = await Task.Run(()=>{return ChunkManager.BuildChunkMesh(_blocks,ChunkPosition.Y == 0);});
        var collisionHull = await Task.Run(()=>{return mesh.CreateTrimeshShape();});
        Update(mesh, collisionHull);
	}

	public override void _Ready() {
		Scale = new Godot.Vector3(VOXEL_SCALE, VOXEL_SCALE, VOXEL_SCALE);
        _chunk_area.AddChild(_chunk_bounding_box);
        AddChild(_chunk_area);
	}

    public static int PackBlockInfo(int blockType) {
        return blockType<<15;
    }

    public static int GetBlockID(int blockInfo) {
        return (blockInfo >> 15) & 0x3ff;
    }

    public static int GetBlockDamageData(int blockInfo) {
        return blockInfo & 0xff;
    }

    public static int GetBlockDamageInteger(int blockInfo) {
        return GetBlockDamageData(blockInfo)&0x1f;
    }

    public static int GetBlockDamageType(int blockInfo) {
        return GetBlockDamageData(blockInfo) >> 5;
    }


    public void SetBlock(int blockIndex, int blockType) {
        _blocks[blockIndex] = (_blocks[blockIndex] & ~(0x3ff << 15)) | blockType << 15;
    }

    public void SetBlockDamageFlag(int blockIndex, BlockDamageType damtype) {
        int damMask = damtype switch  {
            BlockDamageType.Physical => 1<<5,
            BlockDamageType.Fire => 1<<6,
            _ => 1<<7
        };
        _blocks[blockIndex] |= damMask;
    }

    public void SetBlockToAir(int blockIndex) {
        _blocks[blockIndex] = 0;
    }

    public void SetBlockDamagePercentage(int blockIndex, float percentage) {
        _blocks[blockIndex] = (_blocks[blockIndex] & ~0x1f) | Math.Clamp(Mathf.RoundToInt(percentage*31.0),0,31);
    }

    public void SetBlockDamageInteger(int blockIndex, int damage) {
        _blocks[blockIndex] = (_blocks[blockIndex] & ~0x1f) | Math.Clamp(damage,0,31);
    }

    public static BlockSpecies GetBlockSpecies(int blockinfo) {
        return BlockManager.Instance.Blocks[GetBlockID(blockinfo)].Species;
    }

    public static float GetBlockFragility(int blockinfo) {
        return BlockManager.Instance.Blocks[GetBlockID(blockinfo)].Fragility;
    }

    public static bool IsBlockEmpty(int blockInfo) {
        return GetBlockID(blockInfo) == 0;
    }

    public static bool IsBlockInvincible(int blockInfo) {
        return IsBlockEmpty(blockInfo) || (GetBlockID(blockInfo) == BlockManager.Instance.LavaBlockId);
    }

	public int GetBlockInfoFromPosition(Vector3I blockPosition)
	{
        if (blockPosition.X + blockPosition.Z * CHUNK_SIZE + blockPosition.Y * CHUNKSQ >= _blocks.Length) return -1;
		return _blocks[blockPosition.X + blockPosition.Z * CHUNK_SIZE + blockPosition.Y * CHUNKSQ];
	}

	public void Update(Mesh mesh, ConcavePolygonShape3D collisionHull) {
        MeshInstance.Mesh = mesh;
        CollisionShape.Shape = collisionHull;

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

	public void DamageBlocks(List<(Vector3I, int)> blockDamages)
	{ 
        // output dictionary of block positions where particles need to be spawned (for destroyed blocks) and textures for spawned particles
        var particle_spawn_list = new Godot.Collections.Dictionary<Vector3I,int>();

        // array of tuples with block global position as Item1 and damage as Item2
		foreach ((Vector3I,int) blockdamage in blockDamages)
		{
			if (blockdamage.Item1.X < 0 || blockdamage.Item1.X >= Dimensions.X) continue;
			if (blockdamage.Item1.Y < 0 || blockdamage.Item1.Y >= Dimensions.Y*SUBCHUNKS) continue;
			if (blockdamage.Item1.Z < 0 || blockdamage.Item1.Z >= Dimensions.Z) continue;
            
            var block_idx = blockdamage.Item1.X
                + blockdamage.Item1.Z * Dimensions.X
                + blockdamage.Item1.Y * Dimensions.X * Dimensions.Z;

            var blockinfo = _blocks[block_idx];

            //GD.Print("checking if block empty: ", BlockManager.BlockName(blockid));

            if (IsBlockInvincible(blockinfo)) continue; // dont damage air blocks or invincible blocks


            // increase block damage percentage
            var block_damaged = (float)GetBlockDamageInteger(blockinfo);
            block_damaged += blockdamage.Item2*GetBlockFragility(blockinfo);
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
        var mesh = ChunkManager.BuildChunkMesh(_blocks,ChunkPosition.Y == 0);
        var shape = mesh.CreateTrimeshShape();
		Update(mesh, shape);
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
        var blocks_being_destroyed = positionsAndBlockInfo.Count;
        var blockCount = 0;
        var partcount = GetTree().GetNodesInGroup("RigidBreak").Count;
        foreach (var (pos, block_info) in positionsAndBlockInfo) {
            var is_block_above = false;
            var block_idx = Mathf.FloorToInt(pos.X)
            + Mathf.FloorToInt(pos.Z) * CHUNK_SIZE
            + Mathf.FloorToInt(pos.Y) * CHUNKSQ;
            var block_above_idx = block_idx + CHUNKSQ;
            if (block_above_idx <=_blocks.Length) {
                var block_above_id = GetBlockID(_blocks[block_above_idx]);
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
            particles.Position = pos + Godot.Vector3.One*(1/particles.BlockDivisions) - Godot.Vector3.Up*0.0625f; 


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


}