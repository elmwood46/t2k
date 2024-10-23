using Godot;

public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    public Save State { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        State = new Save();
        GD.Print(ProjectSettings.GlobalizePath(Save.SavePath));
        State.LoadSave();
    }

    public void SaveChunk(Vector2I position, Block[,,] blocks)
    {
        State.SaveChunk(position, blocks);
    }

    public Block[,,] LoadChunkOrNull(Vector2I position)
    {
        return State.LoadChunkBlocksOrNull(position);
    }

    public Vector3 LoadPlayerPosition()
    {
        return new Vector3(State.Data.PlayerPosition.Item1,State.Data.PlayerPosition.Item2,State.Data.PlayerPosition.Item3);
    }

    public void SaveGame() {
        State.WriteSave();
    }

    public bool SaveFileExists() {
        return State.SaveFileExists();
    }
}
