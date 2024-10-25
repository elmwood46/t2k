using Godot;

public static class Globals
{
    // Define constants here
    public const float Gravity = 9.81f;
    public const int MaxHealth = 100;
    public const int ChunkSizeX = 16;
    public const int ChunkSizeY = 64;
    public const int ChunkSizeZ = 16;

	public const Input.MouseModeEnum DefaultMouseMode = Input.MouseModeEnum.ConfinedHidden;

    public static readonly Vector3 ChunkDimensions = new Vector3(ChunkSizeX, ChunkSizeY, ChunkSizeZ);

    // Other constants...
    public const string GameVersion = "1.0.0";
    public const string SaveDirectory = "user://saves/";
}