using Godot;
using System;
using System.Collections;

public partial class RigidBreak : Node3D
{
    public float frames = 0f;

    public Vector3 StartingImpulse { get; set; } 

    public int BlockDivisions = 2;

    private ShaderMaterial _shader;

    public float DecayTime { get; set; } = 2.0f;
    public bool MaskHalves { get; set; } = false;
    public bool HalfStrength { get; set; } = false;
    public bool QuarterStrength { get; set; } = false;
    public bool EighthStrength { get; set; } = false;

    public bool OnlyOneParticle { get; set; } = false;

    public Timer t;

    [Export] public Node3D ExplosionCentre { get; set; }

    public RandomNumberGenerator rng = new();
    public StandardMaterial3D BlockMaterial { get; set; }

    public int[] BlockTextures { get; set; }

    public bool NoUpwardsImpulse = false;

    public static readonly PhysicsMaterial physmat = new() {
            Bounce = 0.1f,
            Friction = 0.1f
        };

    public override void _Ready()
    {
        AddToGroup("RigidBreak");

        if (OnlyOneParticle || QuarterStrength || EighthStrength) {
            _shader = (ShaderMaterial)ResourceLoader.Load("res://shaders/broken_block_shader_wholeblock.tres").Duplicate();
        } else {
            _shader = (ShaderMaterial)ResourceLoader.Load("res://shaders/broken_block_shader.tres").Duplicate();
        }

        // pass uniform to shader
        _shader.SetShaderParameter("albedo_texture", BlockManager.Instance.TextureArray);
        _shader.SetShaderParameter("tex_array_idx", BlockTextures);

        Scale = Vector3.One *  (1f/BlockDivisions);
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

        if (OnlyOneParticle)
        {
             var rb = new RigidBody3D
            {
                Position = new Vector3(2f, 2f, 2f)
            };
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = Vector3.One },
                MaterialOverride = _shader,
                GIMode = GeometryInstance3D.GIModeEnum.Dynamic
            };
            var col = new CollisionShape3D
            {
                Shape = new BoxShape3D(){Size = Vector3.One}
            };
            rb.Mass = 10.0f;

            rb.SetCollisionLayerValue(1,false);
            rb.SetCollisionLayerValue(2,true);
            rb.SetCollisionMaskValue(1,true);
            rb.SetCollisionMaskValue(2,true);
            if (MaskHalves) rb.SetCollisionMaskValue(2,false);

            AddChild(rb);
            rb.AddChild(mesh);
            rb.AddChild(col);
        }
        else 
        {
            var i=0;

            var halfStrengthSkip = HalfStrength ? 2 : 1;
            var QuarterStrengthSkip = QuarterStrength ? 2 : 1;
            var EighthStrengthSkip = EighthStrength ? 1 : 1;

            for (int x=0;x<BlockDivisions;x+=1) {
                for (int y=0;y<BlockDivisions;y+=1) {
                    for (int z=0;z<BlockDivisions;z+=1) {
                        i++;
                        // skip the hollow middle blocks
                        if (BlockDivisions == 3) {
                            if ((x== 1) && (z == 1)) {
                                if (y < 2) continue;
                            }
                        } else if (BlockDivisions == 4) {
                            if ((x== 1 || x == 2) && (z == 1 || z == 2)) {
                                if (y < 3) continue;
                            }
                        }else if (BlockDivisions == 5) {
                            if ((x>= 1 || x <= 3) && (z >= 1 || z <= 3)) {
                                if (y < 4) continue;
                            }
                        }
                        if (NoUpwardsImpulse && y > BlockDivisions-2) continue;

                        if (halfStrengthSkip>1 && i%2==0) continue;
                        if (QuarterStrengthSkip>1 && i%3==0) continue;
                        if (QuarterStrengthSkip>1 && i%4==0) continue;

                        var rb = new RigidBody3D
                        {
                            Position = new Vector3(x, y, z) + Vector3.One*0.5f
                        };
                        var mesh = new MeshInstance3D
                        {

                            Mesh = new BoxMesh { Size = Vector3.One },
                            MaterialOverride = _shader,
                            GIMode = GeometryInstance3D.GIModeEnum.Dynamic
                        };
                        var col = new CollisionShape3D
                        {
                            Shape = new BoxShape3D(){Size = Vector3.One}
                        };
                        rb.Mass = 10.0f;

                        rb.SetCollisionLayerValue(1,false);
                        rb.SetCollisionLayerValue(2,true);
                        rb.SetCollisionMaskValue(1,true);
                        rb.SetCollisionMaskValue(2,true);
                        if (MaskHalves) rb.SetCollisionMaskValue(2,false);

                        AddChild(rb);
                        rb.AddChild(mesh);
                        rb.AddChild(col);
                    }
                }
            }
            GD.Print($"created {i} particles");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (frames < 1f) {
            foreach (Node child in GetChildren()) {
                if (child is RigidBody3D rb) {
                    var dir = (ExplosionCentre.GlobalPosition - Player.Instance.GlobalPosition).Normalized();
                    rb.ApplyImpulse(dir * 20f * (NoUpwardsImpulse ? new Vector3(1,0,1) : Vector3.One)) ;
                    rb.ApplyImpulse((rb.Position-ExplosionCentre.Position).Normalized() * 5f * (NoUpwardsImpulse ? new Vector3(1,0,1) : Vector3.One));
                }
            }
            frames ++;
        }

        if ((int)frames %2 == 0) {
            foreach (Node child in GetChildren()) {
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