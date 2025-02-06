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
    private static readonly PackedScene _rigid_break = GD.Load<PackedScene>("res://effects/rigid_break2.tscn");
    private readonly Area3D _chunk_area = new() {Position = new Vector3(ChunkManager.Dimensions.X,0,ChunkManager.Dimensions.Z)*0.5f};
    private readonly CollisionShape3D _chunk_bounding_box = new() {
        Shape = new BoxShape3D { Size = new Vector3(ChunkManager.Dimensions.X, ChunkManager.Dimensions.Y, ChunkManager.Dimensions.Z) }
    };

	// 3d int array for holding blocks
    // each 32bit int contains packed block info: block type (10 bits), z (5 bits), y (5 bits), x (5 bits) 
    // this leaves 7 bits to implement block health or AO
	public Vector3I ChunkPosition { get; private set; } = Vector3I.MaxValue;

	[Export]
	public FastNoiseLite WallNoise { get; set; }

	public override void _Ready() {
		Scale = new Vector3(ChunkManager.VOXEL_SCALE, ChunkManager.VOXEL_SCALE, ChunkManager.VOXEL_SCALE);
        _chunk_area.AddChild(_chunk_bounding_box);
        //_chunk_area.ShapeOwnerSetDisabled((uint)_chunk_area.GetShapeOwners()[0],true);
        _chunk_area.SetCollisionLayerValue(1, true);
        _chunk_area.SetCollisionLayerValue(2, true);
        _chunk_area.SetCollisionLayerValue(3, true);
        _chunk_area.SetCollisionMaskValue(1, true);
        _chunk_area.SetCollisionMaskValue(2, true);
        _chunk_area.SetCollisionMaskValue(3, true);
        AddChild(_chunk_area);
	}
    
    #region set chunk pos
	public void SetChunkPosition(Vector3I position)
	{
        SaveLocalChunkDataAndFree(ChunkPosition);
		ChunkManager.UpdateChunkPosition(this, position, ChunkPosition);
		ChunkPosition = position;
        var newpos = new Vector3(
            ChunkManager.VOXEL_SCALE * ChunkPosition.X * ChunkManager.Dimensions.X,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Y * ChunkManager.Dimensions.Y * ChunkManager.SUBCHUNKS,
            ChunkManager.VOXEL_SCALE * ChunkPosition.Z * ChunkManager.Dimensions.Z);
        CallDeferred(MethodName.SetGlobalPositionAndLoadData, newpos, position);
	}

    public void SaveLocalChunkDataAndFree(Vector3I chunkpos)
    {
        var saved_breakable = new List<DestructibleMeshData>();
        var to_remove = new List<DestructibleMesh>();
        foreach (var child in _chunk_area.GetOverlappingBodies())
        {
            if (child.GetParent().GetParent() is DestructibleMesh d && !d.BrokenScene.Visible)
            {
                var data = new DestructibleMeshData(d);
                saved_breakable.Add(data);
                to_remove.Add(d);
            }
            if (child is RigidBody3D coin)
            {
                coin.QueueFree();
            }
        }
        foreach (var d in to_remove)
        {
            d.Free();
        }
        ChunkManager.Instance.BREAKABLE_MESH_CACHE.TryRemove(chunkpos, out _);
        ChunkManager.Instance.BREAKABLE_MESH_CACHE.TryAdd(chunkpos, saved_breakable);
    }

    public void SetGlobalPositionAndLoadData(Vector3 newpos, Vector3I newchunkpos)
    {
        SetGlobalPosition(newpos);
        if (!ChunkManager.Instance.BREAKABLE_MESH_CACHE.TryGetValue(newchunkpos, out var saved_breakable)) return;
        foreach (DestructibleMeshData data in saved_breakable)
        {
            DestructibleMesh mesh;
            if (data.isChestOpened >= 0)
            {
                var c = new DestructibleChest();
                if (data.isChestOpened == 1) c.SetOpened();
                mesh = c;
            }
            else mesh = new DestructibleMesh();
            mesh.BrokenPacked = data.BrokenPacked;
            mesh.IntactPacked = data.IntactPacked;
            mesh.BrokenScene = data.BrokenPacked.Instantiate() as Node3D;
            mesh.IntactScene = data.IntactPacked.Instantiate() as Node3D;
            mesh.Health = data.Health;
            mesh.MaxHealth = data.MaxHealth;
            mesh.Type = data.Type;
            mesh.PackedBlockDamageInfo = data.PackedBlockDamageInfo;
            
            mesh.AddChild(mesh.BrokenScene);
            mesh.AddChild(mesh.IntactScene);
            // necessary to preserve any scaling or transforms done to the broken scene
            // in the editor
            mesh.BrokenScene.GlobalTransform = data.BrokenTransform;
            ((PhysicsBody3D)mesh.IntactScene.GetChild(0)).GlobalTransform = data.IntactTransform;
            if (mesh is DestructibleChest)
            {
                for (int i=0; i < 6; i++)
                {
                    ((Node3D)mesh.IntactScene.GetChild(0).GetChild(i)).Transform = data.ChestIntactLocalTransforms[i];
                }
            }
            GetTree().Root.AddChild(mesh);

            // chests being loaded should never glow
            // opened chests have less mass
            if (data.isChestOpened == 1) {
                ((DestructibleChest)mesh).TurnGlowOff();
                ((RigidBody3D)mesh.IntactScene.GetChild(0)).Mass = DestructibleChest.MASS_WHEN_OPENED;
            }
        }
    }

    public void UpdateChunkPosition(Vector3I position) {

        SetChunkPosition(position);
        Update();
    }

    #endregion

    #region update

	async public void Update() {
        var meshdata = ChunkManager.TryGetChunkMeshData(ChunkPosition);
        if (meshdata == null) return;

        ArrayMesh meshdata_mesh = new();
        ConcavePolygonShape3D meshdata_shape = new();
        await Task.Run(() => {
            meshdata_mesh = meshdata.GetUnifiedSurfaces();
            meshdata_shape = meshdata.GetTrimeshShape();
        });
        MeshInstance.Mesh = meshdata_mesh;
        CollisionShape.Shape = meshdata_shape;

        CallDeferred(MethodName.UpdateRigidBodies);
	}

    public void UpdateRigidBodies() {
        foreach (Node3D child in _chunk_area.GetOverlappingBodies()) {
            if (child is RigidBody3D rb && rb.Sleeping) {
                //GD.Print("updating rigid body ", rb);
                rb.MoveAndCollide(Vector3.Zero);
            }
        }
    }

    #endregion

    #region broken block particles
    public void SpawnBlockParticles(Godot.Collections.Dictionary<Vector3I, int> positionsAndBlockInfo, Vector3I playerBlockPosition) {
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
                if (partcount + blockCount > 2) particles.MaskHalves = true;
            } else {
                if (partcount + blockCount < 2) particles.BlockDivisions = 20;
                else if (partcount + blockCount < 3) particles.BlockDivisions = 4;
                else if (partcount + blockCount < 4) particles.BlockDivisions = 3;
                if (partcount + blockCount > 3) particles.MaskHalves = true;
            }

            var mult = 3.0f;
            if (partcount > 10*mult || blockCount > 5*mult)
                particles.DecayTime = Mathf.Max(0.5f, 2.0f - partcount * 0.1f);

            /*
            if (!ChunkManager.Instance.DeferredMeshUpdates.IsEmpty) {
                particles.MaskHalves = true;
            }
            else if (blocks_being_destroyed > 1) { // destroying more than one block at once
                if (partcount + blockCount < 1) particles.BlockDivisions = 3;
                if (partcount + blockCount > 1) particles.MaskHalves = true;
            } else {
                if (partcount + blockCount < 2) particles.BlockDivisions = 20;
                else if (partcount + blockCount < 3) particles.BlockDivisions = 4;
                else if (partcount + blockCount < 4) particles.BlockDivisions = 3;
                if (partcount + blockCount > 2) particles.MaskHalves = true;
            }

            particles.HalfStrength = partcount > 5*mult  || blockCount > 5*mult;        
            particles.MaskHalves = partcount >= 8 || blockCount > 6*mult;       
            particles.QuarterStrength = partcount > 10*mult || blockCount > 8*mult;
            particles.EighthStrength = partcount > 12*mult || blockCount > 9*mult;
            particles.OnlyOneParticle = partcount > 14*mult || blockCount > 11*mult;
            */ 

            // set particles position
            // add 1/BLockDivisions to center the particles in the grid (since pos is floored block position)
            // and subtract a small amount to avoid z-fighting with top particles as well as keep them in their block
            // also subtract one to go from padded chunk pos to actual chunk pos
            //particles.Scale = Vector3.One*ChunkManager.VOXEL_SCALE*0.99f;
            particles.Position = pos - Vector3.One*0.5f; //-Vector3.One + Vector3.One*(1/particles.BlockDivisions) - Vector3.Up*0.0625f;

            if (is_block_above) particles.NoUpwardsImpulse = true;

            particles.BlockInfo = block_info;
            
            AddChild(particles);
            blockCount++;
        }
    }
    #endregion

}