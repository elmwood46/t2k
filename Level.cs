using Godot;
using System;

public partial class Level : Node3D
{
    [Export] public Label DisplayInfo;

    public override void _Process(double delta)
    {
        DisplayInfo.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        DisplayInfo.Text += $"\n{Player.Instance}";
    }
}
