using Godot;
using System;

public partial class FallingDeath : StaticBody3D
{
    [Export] float DeathFieldHeight = -50f;
    public override void _Process(double delta)
    {
        base._Process(delta);
        GlobalPosition = new Vector3(Player.Instance.GlobalPosition.X, DeathFieldHeight, Player.Instance.GlobalPosition.Z);
    }
}