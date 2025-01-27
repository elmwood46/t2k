using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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

	public override void _Ready() {
		Scale = new Vector3(ChunkManager.VOXEL_SCALE, ChunkManager.VOXEL_SCALE, ChunkManager.VOXEL_SCALE);
        _chunk_area.AddChild(_chunk_bounding_box);
        AddChild(_chunk_area);
	}

    #region set chunk pos
	public void SetChunkPosition(Vector3I position)
	{
		ChunkManager.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
        var newpos = new Vector3(
            ChunkManager.VOXEL_SCALE * ChunkPosition.X * ChunkManager.Dimensions.X,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Y * ChunkManager.Dimensions.Y * ChunkManager.SUBCHUNKS,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Z * ChunkManager.Dimensions.Z);
        CallDeferred(Node3D.MethodName.SetGlobalPosition, newpos);
	}

    public void UpdateChunkPosition(Vector3I position) {
        SetChunkPosition(position);
        Update();
    }

    #endregion

    #region update

	public void Update() {
        var meshdata = ChunkManager.TryGetChunkMeshData(ChunkPosition);
        if (meshdata == null) return;

        MeshInstance.Mesh = meshdata.GetUnifiedSurfaces();
        CollisionShape.Shape = meshdata.GetTrimeshShape();
        
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

    #region broken block particles
    public void SpawnBlockParticles(Dictionary<Vector3I, int> positionsAndBlockInfo, Vector3I playerBlockPosition) {
        if (positionsAndBlockInfo.Count == 0) return;

        // the particles spawned first have more detail and more expensive collisions\
        var blocks_being_destroyed = positionsAndBlockInfo.Count;
        var blockCount = 0;
        var partcount = GetTree().GetNodesInGroup("RigidBreak").Count;

        var sortedList = positionsAndBlockInfo.Keys.ToList();

        sortedList.Sort((a, b) =>
        {
            float distanceA = a.DistanceSquaredTo(playerBlockPosition);
            float distanceB = b.DistanceSquaredTo(playerBlockPosition);
            return distanceA.CompareTo(distanceB);
        });

        foreach (var pos in sortedList) {
            var block_info = positionsAndBlockInfo[pos];
            var is_block_above = ChunkManager.IsBlockAbove(ChunkPosition,pos);
            var particles = _rigid_break.Instantiate() as RigidBreak;

            // optimize particles to avoid framerate drop         
            if (blocks_being_destroyed > 1) { // destroying more than one block at once
                if (partcount + blockCount < 1) particles.BlockDivisions = 3;
            } else {
                if (partcount + blockCount < 15) particles.BlockDivisions = 4;
                if (partcount + blockCount < 5) particles.BlockDivisions = 20;
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
            // also subtract one to go from padded chunk pos to actual chunk pos
            particles.Scale = Vector3.One*0.99f;
            particles.Position = pos - Vector3.One; /*-Vector3.One + Vector3.One*(1/particles.BlockDivisions) - Vector3.Up*0.0625f;*/ 

            if (is_block_above) particles.NoUpwardsImpulse = true;

            particles.BlockInfo = block_info;
            
            AddChild(particles);
            var impulse_pos = (particles.GlobalTransform.Origin - Player.Instance.GlobalTransform.Origin).Normalized() * 100.0f; 
            particles.StartingImpulse = impulse_pos;
            blockCount++;
        }

    }
    #endregion

}