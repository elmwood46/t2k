using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using MessagePack.Resolvers;

public interface IReloadable
{
    void LoadSavedState();
}

[MessagePackObject]
public class ArrayMeshData
{
    [Key(0)] public List<Dictionary<int, object>> Surfaces { get; set; } = new();
}

[MessagePackObject]
public class SaveData
{
    [Key(0)] public Vector3 PlayerPosition { get; set; }
    [Key(1)] public float HeadYRotation { get; set; }
    [Key(2)] public Dictionary<Vector3I, int[]> SavedBlocks { get; set; } = new();
    [Key(3)] public Dictionary<Vector3I,Dictionary<int,Dictionary<int, List<float>>>> SavedMeshes { get; set; } = new();
    [Key(4)] public HashSet<uint> GeneratedChunks { get; set; } = new();
}

public class SaveState
{
    static SaveState()
    {
        // Configure MessagePack to use the standard resolver with private member support
        var options = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                NativeGuidResolver.Instance,
                StandardResolverAllowPrivate.Instance,
                ContractlessStandardResolver.Instance // add support for common types
            ));  // Use StandardResolverAllowPrivate

        MessagePackSerializer.DefaultOptions = options;
    }

    private SaveData _data = new();

    public const string SAVE_PATH = "user://save.dat";

    public static bool SaveFileExists() => FileAccess.FileExists(SAVE_PATH);

    // Write save data using binary serialization
    public void WriteSave()
    {
        try
        {
            // Serialize the SaveData object to a byte array using MessagePack
            byte[] saveData = MessagePackSerializer.Serialize(_data);

            try
            {
                // Write the byte array to the file
                System.IO.File.WriteAllBytes(ProjectSettings.GlobalizePath(SAVE_PATH), saveData);

                GD.Print($"SAVEDATA File saved successfully at: {ProjectSettings.GlobalizePath(SAVE_PATH)}");
            }
            catch (Exception e)
            {
                GD.PrintErr($"Failed to write save data to file: {e.Message}");
                GD.PrintErr($"Stack trace: {e.StackTrace}");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to serialize save data: {e.Message}");
            GD.PrintErr($"Stack trace: {e.StackTrace}");
        }
    }

    // Load save data using binary serialization
    public SaveData LoadSave()
    {
        if (!SaveFileExists())
        {
            GD.Print("Called LoadSave(): No save file found.");
            
            return null;
        }

        try
        {
            // Read the byte array from the file
            byte[] saveData = System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(SAVE_PATH));

            // Deserialize the byte array to a SaveData object using MessagePack
            _data = MessagePackSerializer.Deserialize<SaveData>(saveData);

            GD.Print("_data loaded successfully.");
            return _data;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load data: {e.Message}");
            return null;
        }
    }

    public void CachePlayerInformation()
    {
        if (Player.Instance == null)
        {
            GD.PrintErr("Player instance is null. Cannot cache player information.");
            return;
        }
        _data.PlayerPosition = Player.Instance.GlobalPosition;
        _data.HeadYRotation = Player.Instance.Head.Rotation.Y;
    }

    public void CacheWorldData()
    {
        _data.SavedBlocks = ChunkManager.Instance.BLOCKCACHE.ToDictionary(pair => pair.Key, pair => pair.Value);
        _data.SavedMeshes = ChunkManager.Instance.MESHCACHE.ToDictionary(pair => pair.Key, pair => pair.Value.SerializeSurfaceData());
        _data.GeneratedChunks = CantorPairing.GetSet();
    }

    public Vector3 GetCachedPlayerPosition() => _data.PlayerPosition;
    public float GetCachedHeadYRotation() => _data.HeadYRotation;
    public Dictionary<Vector3I, int[]> GetCachedBlocks() => _data.SavedBlocks;
    public Dictionary<Vector3I,Dictionary<int,Dictionary<int, List<float>>>> GetCachedMeshes() => _data.SavedMeshes;
    public HashSet<uint> GetCachedCantorPairings() => _data.GeneratedChunks;
    public SaveData GetCachedData() => _data;
}