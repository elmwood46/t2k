using Godot;
using System;
using System.Collections;

public partial class RigidBreak : Node3D
{
    public float frames = 0f;

    public Vector3 StartingImpulse { get; set; }

    public int BlockDivisions = 2;

    private readonly ShaderMaterial _shader = (ShaderMaterial)BlockManager.Instance.BrokenBlockShader.Duplicate();

    private Node3D _BrokenScene { get; set; }

    public static readonly PackedScene CubeBroken20Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-20.tscn");
    public static readonly PackedScene CubeBroken3Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-3.tscn");
    public static readonly PackedScene CubeBroken4Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-4.tscn");
    public static readonly PackedScene CubeBroken5Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-5.tscn");

    public float DecayTime { get; set; } = 2.0f;
    public bool MaskHalves { get; set; } = false;
    public bool HalfStrength { get; set; } = false;
    public bool QuarterStrength { get; set; } = false;
    public bool EighthStrength { get; set; } = false;
    public bool OnlyOneParticle { get; set; } = false;

    public Timer t;

    [Export] public Node3D ExplosionCentre { get; set; }

    public RandomNumberGenerator rng = new();

    public int BlockInfo { get; set; }

    public bool NoUpwardsImpulse = false;

    public static readonly PhysicsMaterial physmat = new() {
            Bounce = 0.1f,
            Friction = 0.1f
        };

    public override void _Ready()
    {
        AddToGroup("RigidBreak");

        // pass uniforms to shader
        _shader.SetShaderParameter("_tex_array_idx", BlockManager.BlockTextureArrayPositions(ChunkManager.GetBlockID(BlockInfo)));
        _shader.SetShaderParameter("_damage_data", ChunkManager.GetBlockDamageData(BlockInfo));

        bool blockIsSloped = ChunkManager.IsBlockSloped(BlockInfo);

        //DEBUG dont break sloped blocks
        if (blockIsSloped) {
            QueueFree();
            return;
        }

        if (BlockDivisions <= 2) {
            _BrokenScene = CubeBroken3Fragments.Instantiate() as Node3D;
            AddChild(_BrokenScene);
        } else if (BlockDivisions == 3) {
            _BrokenScene = CubeBroken4Fragments.Instantiate() as Node3D;
            AddChild(_BrokenScene);
        } else if (BlockDivisions == 4) {
            _BrokenScene = CubeBroken5Fragments.Instantiate() as Node3D;
            AddChild(_BrokenScene);
        } else {
            _BrokenScene = CubeBroken20Fragments.Instantiate() as Node3D;
            AddChild(_BrokenScene);
        }

        //Scale = Vector3.One *  (1f/BlockDivisions);
        t = new Timer
        {
            Autostart = false,
            WaitTime = DecayTime
        };
         t.Timeout += () => {
            RemoveFromGroup("RigidBreak");
            QueueFree();
        };
        AddChild(t);
        t.Start();


        foreach (Node child in _BrokenScene.GetChildren()) {
            if (child is RigidBody3D rb) {
                rb.SetCollisionLayerValue(1, false);
                rb.SetCollisionLayerValue(2, true);
                rb.SetCollisionMaskValue(1, true);
                rb.SetCollisionMaskValue(2, true);
                if (MaskHalves) rb.SetCollisionMaskValue(2,false);
                foreach (Node meshchild in rb.GetChildren()) {
                    if (meshchild is MeshInstance3D mesh) {
                        mesh.MaterialOverride = _shader;
                    }
                }
                rb.Freeze = false;
                if (OnlyOneParticle) break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_BrokenScene == null) return;

        if (frames < 1f) {
            foreach (Node child in _BrokenScene.GetChildren()) {
                if (child is RigidBody3D rb) {
                    var dir = (ExplosionCentre.GlobalPosition - Player.Instance.GlobalPosition).Normalized();
                    rb.ApplyImpulse(dir * 20f * (NoUpwardsImpulse ? new Vector3(1,0,1) : Vector3.One)) ;
                    rb.ApplyImpulse((rb.Position-ExplosionCentre.Position).Normalized() * 5f * (NoUpwardsImpulse ? new Vector3(1,0,1) : Vector3.One));
                }
            }
            frames ++;
        }

        if ((int)frames %2 == 0) {
            foreach (Node child in _BrokenScene.GetChildren()) {
                if (child is RigidBody3D rb) {
                    if (t.TimeLeft < DecayTime/4) {
                        rb.Scale = (float)Mathf.Max(t.TimeLeft/(DecayTime/4),0.1d)*Vector3.One;
                    }
                    else {
                        rb.MoveAndCollide(rb.LinearVelocity*(float)delta);
                    }
                }
            }
        }

        frames++;
    }
}