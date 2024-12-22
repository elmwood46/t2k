using System;
using System.Diagnostics;
using Godot;

public partial class DebugOverlay : Control
{
    readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    [Export] public RichTextLabel DebugLog {get; set;}

    [Export] public RichTextLabel PlayerInfo {get; set;}

    [Export] public Label StopwatchLabel {get; set;}

    // add text to debug overlay
    public override void _Process(double delta) {

        StopwatchLabel.Text = $"Elapsed Time: {_stopwatch.Elapsed}";
        PlayerInfo.Text = "\n";
        PlayerInfo.Text += $"PlayerPosition: {Player.Instance.Position}\n";
        PlayerInfo.Text += "PlayerChunkPosition: ";
        PlayerInfo.Text += $"{Mathf.FloorToInt(Player.Instance.Position.X/Chunk.Dimensions.X)},";
        PlayerInfo.Text += $"{Mathf.FloorToInt(Player.Instance.Position.Z/Chunk.Dimensions.Z)}\n";
        //PlayerInfo.Text += $"Player State: {Player.State}\n";
        //PlayerInfo.Text += $"Stun Timer: {Player.Instance.GetStunTimeLeft()}\n";
    }
}