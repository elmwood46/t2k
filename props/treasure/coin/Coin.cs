using Godot;
using System;

public partial class Coin : RigidBody3D
{
    // time in seconds before the coin despawns
    const double COIN_LIFETIME = 60;

    private Timer _deathtimer = new(){WaitTime = 1, Autostart = false, OneShot = true};

    public bool MoveTowardPlayer = false;

    private const float _lerpfactor = 0.1f;

    private Vector3 _base_scale; 

    public override void _Ready()
    {
        _base_scale = ((MeshInstance3D)GetChild(0)).Scale;
        AddChild(_deathtimer);
        var _lifetime = new Timer() {
            WaitTime = COIN_LIFETIME,
            Autostart = false,
            OneShot = true
        };
        _lifetime.Timeout += () => {
            _deathtimer.Start();
        };
        _deathtimer.Timeout += () => {
            CallDeferred(MethodName.QueueFree);
        };
        AddChild(_lifetime);
        _lifetime.Start();
    }

    public override void _PhysicsProcess(double delta)
    {        
        if (!_deathtimer.IsStopped())
        {
            ((MeshInstance3D)GetChild(0)).Scale = _base_scale*(float)Math.Max(_deathtimer.TimeLeft/_deathtimer.WaitTime,0.1f);
            ((CollisionShape3D)GetChild(1)).Scale = _base_scale*(float)Math.Max(_deathtimer.TimeLeft/_deathtimer.WaitTime,0.1f);
        }

        if (MoveTowardPlayer && Player.Instance != null)
        {
            SetCollisionMaskValue(1,false);
            float dx,dy,dz;
            dx = Mathf.Lerp(GlobalPosition.X,Player.Instance.Head.GlobalPosition.X,_lerpfactor);
            dy = Mathf.Lerp(GlobalPosition.Y,Player.Instance.Head.GlobalPosition.Y,_lerpfactor);
            dz = Mathf.Lerp(GlobalPosition.Z,Player.Instance.Head.GlobalPosition.Z,_lerpfactor);
            GlobalPosition = new Vector3(dx,dy,dz);
            if (GlobalPosition.DistanceSquaredTo(Player.Instance.Head.GlobalPosition) <= 2.0f)
            {
                Player.AddMoney(1);
                CallDeferred(MethodName.QueueFree);
            }
        }
    }
}
