using System.Collections.Generic;
using Godot;

public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    private SaveState _state;

    public override void _Ready()
    {
        Instance = this;
        _state = new SaveState();
        GD.Print(ProjectSettings.GlobalizePath(SaveState.SAVE_PATH));
    }

    public static Vector3 GetCachedPlayerPosition() => Instance._state.GetCachedPlayerPosition();
    public static float GetCachedHeadYRotation() => Instance._state.GetCachedHeadYRotation();
    public static Dictionary<Vector3I, int[]> GetCachedBlocks() => Instance._state.GetCachedBlocks();
    public static Dictionary<Vector3I, ChunkMeshData> GetCachedMeshes() {
        var ret = new Dictionary<Vector3I, ChunkMeshData>();
        var meshdata_dict = Instance._state.GetCachedMeshes();
        foreach (var (idx, data) in meshdata_dict) {
            ret[idx] = new ChunkMeshData(data);
        }
        return ret;
    }

    public static HashSet<uint> GetCachedCantorPairings() => Instance._state.GetCachedCantorPairings();
    public static SaveData GetCachedData() => Instance._state.GetCachedData();

    public static void CacheCurrentWorldState() {
        Instance._state.CachePlayerInformation();
        Instance._state.CacheWorldData();
    }

    public static void WriteSaveCache() {
        Instance._state.WriteSave();
    }

    public static void LoadSaveCache() {
        Instance._state.LoadSave();
    }

    public static bool SaveFileExists() {
        return SaveState.SaveFileExists();
    }

    public static void SaveToFile() {
        CacheCurrentWorldState();
	    WriteSaveCache();
    }

    public static void LoadSavedState() {
        LoadSaveCache();
        Player.Instance.LoadSavedState();
        ChunkManager.Instance.LoadSavedState();
    }
}
