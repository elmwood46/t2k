using Godot;
using System;

public partial class DestructibleRedCrate : DestructibleMesh
{
    [Export] public float FuseTime = 5.0f;
    private static readonly PackedScene _explosion_scene = ResourceLoader.Load<PackedScene>("res://effects/red_barrel_explode.tscn"); 
    private static readonly AudioStream _bomb_beep_sound = ResourceLoader.Load<AudioStream>("res://audio/bomb_beep.ogg");
    private AudioStreamPlayer3D _bomb_beep_player;
    private Timer _fuse_timer;
    private Timer _flash_timer;
    private double _prev_time = 0f;

    public override void _Ready()
    {
        _shaderMaterial = (ShaderMaterial)BlockManager.Instance.DestructibleObjectShader.Duplicate();
        
        base._Ready();
        
        _bomb_beep_player = new AudioStreamPlayer3D
        {
            Stream = _bomb_beep_sound
        };
        IntactScene.GetChild(0).AddChild(_bomb_beep_player);
        _flash_timer = new Timer(){Autostart = false, OneShot = true, WaitTime = 1.0f};
        _fuse_timer = new Timer(){Autostart = false, OneShot = true, WaitTime = FuseTime};
        AddChild(_flash_timer);
        AddChild(_fuse_timer);
        _fuse_timer.Timeout += FuseTimeout;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (_is_broken) return;

        if (_fuse_timer.IsStopped() && Health < MaxHealth)
        {
            _fuse_timer.Start();
            _prev_time = _fuse_timer.TimeLeft;
        }

        if (!_fuse_timer.IsStopped()) 
        {
            if (_fuse_timer.TimeLeft <= _prev_time)
            {
                var beep_interval = 0.2*_fuse_timer.TimeLeft;
                _prev_time = _fuse_timer.TimeLeft - beep_interval;
                _bomb_beep_player.Play();

                _flash_timer.Stop();
                _flash_timer.WaitTime = beep_interval;
                _flash_timer.Start();
            }
        }

        if (!_flash_timer.IsStopped())
        {
            var crate_shader = ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).MaterialOverride as ShaderMaterial;
            crate_shader.SetShaderParameter("_fuse_is_active", true);
            crate_shader.SetShaderParameter("_fuse_ratio", (float)(1.0-_flash_timer.TimeLeft/_flash_timer.WaitTime));
            ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).MaterialOverride = crate_shader;
        }
    }

    private void FuseTimeout()
    {
        _flash_timer.Stop();
        var expl = _explosion_scene.Instantiate() as Explosion;
        var pos = ((Node3D)IntactScene.GetChild(0)).GlobalPosition;
        AddSibling(expl);
        expl.GlobalPosition = pos;
        expl.Scale = 2.0f*Vector3.One;
        var crate_shader = ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).MaterialOverride as ShaderMaterial;
        crate_shader.SetShaderParameter("_fuse_is_active", false);
        crate_shader.SetShaderParameter("_fuse_ratio", 0.0);
        ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).MaterialOverride = crate_shader;
        Break(pos,expl.ExplosionForce);
    }
}
