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


    public static readonly PackedScene CubeBroken3Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-3.tscn");
    public static readonly PackedScene CubeBroken4Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-3.tscn");
    public static readonly PackedScene CubeBroken5Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-4.tscn");

    // the UV scale for fracture cube 20 is wrong btw, probably because it was the first one I made
    public static readonly PackedScene CubeBroken20Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/fracture-cube-20.tscn");

    // HACK set all the sideslopes to 20 because it's the only one where the UVs aren't fucky
    // this was due to some blender exporting error no doubt
    // it may be simply because there were more subdivisions the UVs didnt get sliced up weirdly
    // uvs arent broken for other fragments, just the sideslopes 3-5
    public static readonly PackedScene SideSlope3Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-sideslope-20-3-alt.tscn");
    public static readonly PackedScene SideSlope4Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-sideslope-20-3-alt.tscn");
    public static readonly PackedScene SideSlope5Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-sideslope-20-3-alt.tscn");
    public static readonly PackedScene SideSlope20Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-sideslope-20.tscn");
    public static readonly PackedScene InvCorner3Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-invcorner-3.tscn");
    public static readonly PackedScene InvCorner4Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-invcorner-3.tscn");
    public static readonly PackedScene InvCorner5Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-invcorner-4.tscn");
    public static readonly PackedScene InvCorner20Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-invcorner-20.tscn");
    public static readonly PackedScene Corner3Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-corner-3.tscn");
    public static readonly PackedScene Corner4Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-corner-3.tscn");
    public static readonly PackedScene Corner5Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-corner-4.tscn");
    public static readonly PackedScene Corner20Fragments = ResourceLoader.Load<PackedScene>("res://props/breakable/slopes/fracture-corner-20.tscn");

    // order the scenes the same as the BlockSlopeType enum
    // 0-3 are cube fragments, 4-7 are side slope fragments, 8-11 are corner fragments, 12-15 are inverted corner fragments
    private static readonly PackedScene[] FragmentScenes = {
        CubeBroken3Fragments,
        CubeBroken4Fragments,
        CubeBroken5Fragments,
        CubeBroken20Fragments,
        SideSlope3Fragments,
        SideSlope4Fragments,
        SideSlope5Fragments,
        SideSlope20Fragments,
        Corner3Fragments,
        Corner4Fragments,
        Corner5Fragments,
        Corner20Fragments,
        InvCorner3Fragments,
        InvCorner4Fragments,
        InvCorner5Fragments,
        InvCorner20Fragments
    };

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
        var slopeType = ChunkManager.GetBlockSlopeType(BlockInfo);
        var slopeRotation = ChunkManager.GetBlockSlopeRotation(BlockInfo);

        _BrokenScene = FragmentScenes[Mathf.Clamp(BlockDivisions-2,0,3)+slopeType*4].Instantiate() as Node3D;
        if (blockIsSloped) {
            _BrokenScene.RotateY(slopeRotation);
            _BrokenScene.Position += new Vector3(0.5f,0.0f,0.5f);
        }
        AddChild(_BrokenScene);

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

        foreach (Node child in _BrokenScene.GetChildren())
        {
            if (child is RigidBody3D rb)
            {
                rb.SetCollisionLayerValue(1, false);
                rb.SetCollisionLayerValue(2, true);
                rb.SetCollisionMaskValue(1, true);
                rb.SetCollisionMaskValue(2, true);
                if (MaskHalves) rb.SetCollisionMaskValue(2,false);
                foreach (Node meshchild in rb.GetChildren())
                {
                    if (meshchild is MeshInstance3D mesh)
                    {
                        mesh.MaterialOverride = _shader;
                    }
                }
                rb.Freeze = false;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_BrokenScene == null) return;

        if (frames < 1f)
        {
            foreach (Node child in _BrokenScene.GetChildren())
            {
                if (child is RigidBody3D rb)
                {
                    var dir = (ExplosionCentre.GlobalPosition - Player.Instance.GlobalPosition).Normalized();
                    rb.ApplyImpulse(dir * 20f * (NoUpwardsImpulse ? new Vector3(1,0,1) : Vector3.One)) ;
                    rb.ApplyImpulse((rb.Position-ExplosionCentre.Position).Normalized() * 5f * (NoUpwardsImpulse ? new Vector3(1,0,1) : Vector3.One));
                }
            }
            frames ++;
        }

        if ((int)frames %2 == 0)
        {
            foreach (Node child in _BrokenScene.GetChildren())
            {
                if (child is RigidBody3D rb)
                {
                    if (t.TimeLeft < DecayTime/4)
                    {
                        rb.Scale = (float)Mathf.Max(t.TimeLeft/(DecayTime/4),0.1d)*Vector3.One;
                    }
                    else
                    {
                        rb.MoveAndCollide(rb.LinearVelocity*(float)delta);
                    }
                }
            }
        }

        frames++;
    }
}