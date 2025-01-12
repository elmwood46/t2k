using Godot;
using System;

[Tool]
public partial class DestructibleMesh : Node3D
{
    [Export] public Node3D IntactScene { get; set; }
    [Export] public Node3D BrokenScene{ get; set; }

    [Export] public float DecayTime { get; set; } = 3.0f;

    private Timer _t;

    public override void _Ready()
    {
        BrokenScene.Visible = false;
    }

    public void Break(Vector3 collisionPoint, float force) {
        if (!BrokenScene.Visible) {
            BrokenScene.Position = ((RigidBody3D)IntactScene.GetChild(0)).Position;
            BrokenScene.Rotation = ((RigidBody3D)IntactScene.GetChild(0)).Rotation;
            BrokenScene.Visible = true;

            foreach (Node child in BrokenScene.GetChildren()) {
                if (child is RigidBody3D rb) {
                    rb.SetCollisionLayerValue(1, false);
                    rb.SetCollisionLayerValue(2, true);
                    rb.SetCollisionMaskValue(1, true);
                    rb.SetCollisionMaskValue(2, true);
                    rb.Freeze = false;
                    var force_dir = collisionPoint.DirectionTo(rb.GlobalPosition);
                    rb.LinearVelocity = ((RigidBody3D)IntactScene.GetChild(0)).LinearVelocity;
                    rb.ApplyImpulse(force_dir*force);
                }
            }

            IntactScene.Visible = false;
            IntactScene.QueueFree();

            _t = new Timer
            {
                Autostart = false,
                WaitTime = DecayTime
            };
            _t.Timeout += () => {
                QueueFree();
            };
            AddChild(_t);
            _t.Start();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (BrokenScene.Visible && _t.TimeLeft < DecayTime/4) {
            foreach (Node child in BrokenScene.GetChildren()) {
                if (child is RigidBody3D rb) {
                    rb.Scale = (float)Mathf.Max(_t.TimeLeft/(DecayTime/4),0.1d)*Vector3.One;
                }
            }
        }
    }
}