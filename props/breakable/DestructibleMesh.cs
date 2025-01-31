using Godot;
using System;
using System.Security.Cryptography;
using RandomNumberGenerator = Godot.RandomNumberGenerator;

public interface IHurtable
{
    void TakeDamage(int damage);
}

public partial class DestructibleMesh : Area3D, IHurtable
{
    [Export] public PackedScene IntactPacked {get ; set;}
    [Export] public PackedScene BrokenPacked {get; set;}
    [Export] public Node3D IntactScene { get; set; }
    [Export] public Node3D BrokenScene { get; set; }
    [Export] public float DecayTime { get; set; } = 3.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public Texture2D Texture { get; set; }
    public float Health = -1;
    private ShaderMaterial _shaderMaterial;
    private Vector3 _base_scale;
    private Vector3 _base_position;
    private double _max_hit_time = 0.5f;
    private double _hit_time = 0.0;
    private float _hit_anim_lerp = 0.1f;
    private float _max_scale_factor = 1.05f;
    private float _max_shake_factor = 0.05f;
    private float _shake_factor = 0.0f;
    private int _packed_damage_data_blockinfo = 0;

    private static readonly PackedScene _break_particles = ResourceLoader.Load<PackedScene>("res://props/stones/break_object_particles.tscn");

    private RandomNumberGenerator rng = new();

    private Timer _t;

    public override void _Ready()
    {
        _base_scale = Scale;
        _base_position = Position;
        if (Health == -1) Health = MaxHealth;
        BrokenScene.Visible = false;
        _shaderMaterial = (ShaderMaterial)BlockManager.Instance.DestructibleObjectShader.Duplicate();
        _shaderMaterial.SetShaderParameter("_texture_albedo", Texture);
        _shaderMaterial.SetShaderParameter("_damage_data", 0);
        if (IntactScene.GetChild(0).GetChild(0) is MeshInstance3D m) {
            m.MaterialOverride = _shaderMaterial;
        }
    }

    public void TakeDamage(int damage)
    {
        Health -= damage;
        if (Health <= 0) Health = 0;
        var healthfact = 1.0f-Health/MaxHealth;
        _packed_damage_data_blockinfo = ChunkManager.RepackDamageData(_packed_damage_data_blockinfo, ChunkManager.PackDamageFlag(BlockDamageType.Physical), Mathf.RoundToInt(31.0f*healthfact));
        GD.Print($"set damage to: {Mathf.RoundToInt(31.0f*healthfact)}");
        _shaderMaterial.SetShaderParameter("_damage_data", ChunkManager.GetBlockDamageData(_packed_damage_data_blockinfo));

        if (IntactScene.GetChild(0) is not StaticBody3D) return;
        _hit_time = _max_hit_time;

        float scalex, scaley, scalez;
        scalex = Mathf.Lerp(_base_scale.X,_base_scale.X*_max_scale_factor,healthfact);
        scaley = Mathf.Lerp(_base_scale.Y,_base_scale.Y*_max_scale_factor,healthfact);
        scalez = Mathf.Lerp(_base_scale.Z,_base_scale.Z*_max_scale_factor,healthfact);
        Scale = new Vector3(scalex,scaley,scalez);

        _shake_factor = Mathf.Lerp(0.0f,_max_shake_factor,healthfact);
    }

    public void Deactivate()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Disabled;
    }

    public void Reactivate()
    {
        Visible = true;
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public void Break(Vector3 collisionPoint, float force) {
        var part = _break_particles.Instantiate() as GpuParticles3D;
        AddChild(part);
        part.Emitting = true;
        if (!BrokenScene.Visible) {
            BrokenScene.Position = ((Node3D)IntactScene.GetChild(0)).Position;
            BrokenScene.Rotation = ((Node3D)IntactScene.GetChild(0)).Rotation;
            BrokenScene.Visible = true;

            if (IntactScene.GetChild(0) is PhysicsBody3D pb)
            {
                pb.SetCollisionLayerValue(1, false);
                pb.SetCollisionMaskValue(1, false);
                pb.SetCollisionLayerValue(2, false);
                pb.SetCollisionMaskValue(2, false);
            }

            foreach (Node child in BrokenScene.GetChildren()) {
                if (child is RigidBody3D rb) {
                    if (rb.GetChild(0) is MeshInstance3D m)
                    {
                        m.MaterialOverride = _shaderMaterial;
                    }
                    rb.SetCollisionLayerValue(1, false);
                    rb.SetCollisionLayerValue(2, true);
                    rb.SetCollisionMaskValue(1, true);
                    rb.SetCollisionMaskValue(2, true);
                    rb.Freeze = false;
                    var force_dir = collisionPoint.DirectionTo(rb.GlobalPosition);
                    if (IntactScene.GetChild(0) is RigidBody3D intact_rb) {
                        rb.LinearVelocity = intact_rb.LinearVelocity;
                    }
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
            if (Scale != _base_scale) Scale = _base_scale;
            if (Position != _base_position) Position = _base_position;
            foreach (Node child in BrokenScene.GetChildren()) {
                if (child is RigidBody3D rb) {
                    rb.Scale = (float)Mathf.Max(_t.TimeLeft/(DecayTime/4),0.1d)*Vector3.One;
                }
            }
        }
        else 
        {
            if (_hit_time > 0) 
            {
                // lerp scale back to normal
                float scalex, scaley, scalez;
                scalex = Mathf.Lerp(Scale.X,_base_scale.X,_hit_anim_lerp);
                scaley = Mathf.Lerp(Scale.Y,_base_scale.Y,_hit_anim_lerp);
                scalez = Mathf.Lerp(Scale.Z,_base_scale.Z,_hit_anim_lerp);
                Scale = new Vector3(scalex,scaley,scalez);

                var sf = _shake_factor*(float)(_hit_time / _max_hit_time);
                var randvec = new Vector3(rng.RandfRange(-sf,sf),rng.RandfRange(-sf,sf),rng.RandfRange(-sf,sf));

                Position = _base_position + randvec * Mathf.Sin((float)_hit_time);

                _hit_time-=delta;
            }
        }
    }
}