using Godot;
using System;

public partial class PointsBar : Control
{
    [Export]
    Label label;

    [Export]
    ProgressBar progressBar;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
		GamePoints.Instance.Updated += OnGamePointsUpdated;
		label.Text = "level " + 1;
		progressBar.MaxValue = GamePoints.MaxPoints;
		progressBar.MinValue = 0;
    }

    private void OnGamePointsUpdated(int points, int level)
    {
        label.Text = "level " + level;
        progressBar.Value = points;
    }
}