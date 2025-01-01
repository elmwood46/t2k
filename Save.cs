using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using MessagePack.Resolvers;

public partial class Save
{

    static Save()
    {
        // Configure MessagePack to use the standard resolver with private member support
        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                NativeGuidResolver.Instance,
                StandardResolverAllowPrivate.Instance));  // Use StandardResolverAllowPrivate

        MessagePackSerializer.DefaultOptions = options;
    }

    [MessagePackObject]
    public class SaveData
    {
        [Key(0)] public (float,float,float) PlayerPosition { get; set; }
        [Key(1)] public float HeadRotation { get; set; }
        [Key(2)] public Dictionary<(int,int), int[]> Chunks { get; set; } = new Dictionary<(int,int),int[]>();
        [Key(3)] public HashSet<(int,int)> allChunks { get; set; } = new HashSet<(int,int)>();
    }

    public SaveData Data { get; private set; }

    public const string SavePath = "user://save.dat";

    private int ParseBlockToInt(Block block) {
        return BlockManager.BlockID("Air");/*
        if (block == BlockManager.Instance.Air) return (int)BID.Air;
        else if (block == BlockManager.Instance.Stone) return (int)BID.Stone;
        else if (block == BlockManager.Instance.Dirt) return (int)BID.Dirt;
        else if (block == BlockManager.Instance.Grass) return (int)BID.Grass;
        else if (block == BlockManager.Instance.Leaves) return (int)BID.Leaves;
        else if (block == BlockManager.Instance.Trunk) return (int)BID.Trunk;
        else if (block == BlockManager.Instance.Brick) return (int)BID.Brick;
        else if (block == BlockManager.Instance.Lava) return (int)BID.Lava;
        else throw new Exception($"Block {block} not found in BlockManager.");*/
    }

    private Block ParseIntToBlock(int i) {
        return new Block();
        /*
        if (i == (int)BID.Air) return BlockManager.Instance.Air;
        else if (i == (int)BID.Stone) return BlockManager.Instance.Stone;
        else if (i == (int)BID.Dirt) return BlockManager.Instance.Dirt;
        else if (i == (int)BID.Grass) return BlockManager.Instance.Grass;
        else if (i == (int)BID.Leaves) return BlockManager.Instance.Leaves;
        else if (i == (int)BID.Trunk) return BlockManager.Instance.Trunk;
        else if (i == (int)BID.Brick) return BlockManager.Instance.Brick;
        else if (i == (int)BID.Lava) return BlockManager.Instance.Lava;
        else throw new Exception($"Block {i} not found in BlockManager.");*/
    }

    public bool SaveFileExists() => Godot.FileAccess.FileExists(SavePath);

    // Write save data using binary serialization
    public void WriteSave()
    {
        try
        {
            // Serialize the SaveData object to a byte array using MessagePack
            byte[] saveData = MessagePackSerializer.Serialize(Data);

            // Write the byte array to the file
            System.IO.File.WriteAllBytes(ProjectSettings.GlobalizePath(SavePath), saveData);

            GD.Print($"File saved successfully at: {ProjectSettings.GlobalizePath(SavePath)}");

        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to save data: {e.Message}");

        }
    }

    // Load save data using binary serialization
    public SaveData LoadSave()
    {
        if (!SaveFileExists())
        {
            GD.Print("Called LoadSave(): No save file found. Setting up new save data.");


            Data = new SaveData();
            return null;
        }

        try
        {
            // Read the byte array from the file
            byte[] saveData = System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(SavePath));

            // Deserialize the byte array to a SaveData object using MessagePack
            Data = MessagePackSerializer.Deserialize<SaveData>(saveData);

            GD.Print("Data loaded successfully.");
            return Data;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load data: {e.Message}");
            return null;
        }
    }

    public void SavePlayerInformation(Vector3 position, float headRotation)
    {
        Data.PlayerPosition = (position.X, position.Y, position.Z);
        Data.HeadRotation = headRotation;
    }

    public void SaveChunk(Vector2I position, Block[,,] blocks)
    {
        int[] blockList = blocks.Cast<Block>().Select(block => ParseBlockToInt(block)).ToArray();
        Data.Chunks[(position.X,position.Y)] = blockList;
        //GD.Print($"Stored chunk at {position}");

    }

    public Block[,,] LoadChunkBlocksOrNull(Vector2I position)
    {
        // get block ids for the chunk
        if (!Data.Chunks.ContainsKey((position.X,position.Y))) return null;
        int[] bids = Data.Chunks[(position.X,position.Y)];

        // fill 3d array with blocks
        Block[,,] blocks = new Block[Chunk.Dimensions.X, Chunk.Dimensions.Y, Chunk.Dimensions.Z];
        for (int x = 0; x < Chunk.Dimensions.X; x++)
        {
            for (int y = 0; y < Chunk.Dimensions.Y; y++)
            {
                for (int z = 0; z < Chunk.Dimensions.Z; z++)
                {
                    int index = x * Chunk.Dimensions.Y * Chunk.Dimensions.Z + y * Chunk.Dimensions.Z + z;
                    blocks[x, y, z] = ParseIntToBlock(bids[index]);
                }
            }
        }

        //GD.Print($"Loaded chunk at {position}");
        return blocks;
    }
}