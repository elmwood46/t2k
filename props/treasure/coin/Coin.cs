using Godot;
using System;
using System.Collections.Generic;

public partial class Coin : RigidBody3D
{
    // time in seconds before the coin despawns
    const double COIN_LIFETIME = 60;

    private Timer _deathtimer = new(){WaitTime = 1, Autostart = false, OneShot = true};
    private Timer _lifetime = new() {WaitTime = COIN_LIFETIME,Autostart = false,OneShot = true};

    public bool MoveTowardPlayer = false;

    private const float _lerpfactor = 0.1f;

    private Vector3 _base_scale; 

    private static readonly AudioStream[] _coin_fall_sounds = new AudioStream[]
    {
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_2.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_3.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_4.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_5.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_6.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_7.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_8.ogg"),
        ResourceLoader.Load<AudioStream>("res://audio/coins/coin_fall_9.ogg"),
    };

    public static readonly AudioStream PickupSound = ResourceLoader.Load<AudioStream>("res://audio/coins/coin_pickup.ogg"); 

    public override void _Ready()
    {
        Visible = false;
        Freeze = true;
        BodyEntered += (body) => {
            if (!Freeze && LinearVelocity.LengthSquared() > 0.2f && body is StaticBody3D || body is RigidBody3D)
            {
                var stream = _coin_fall_sounds[GD.RandRange(0,_coin_fall_sounds.Length-1)];
                AudioManager.TryPlay(stream, AudioBus.Misc, GlobalPosition);
            }
        };

        SetCollisionLayerValue(1,false);
        SetCollisionLayerValue(2,false);
        SetCollisionLayerValue(3,true);
        SetCollisionMaskValue(1,true);
        SetCollisionMaskValue(2,false);
        SetCollisionMaskValue(3,true);
        SetCollisionMaskValue(9,true);

        _base_scale = ((MeshInstance3D)GetChild(0)).Scale;
        _lifetime.Timeout += () => {
            _deathtimer.Start();
        };
        _deathtimer.Timeout += () => {
            Deactivate();
        };
        AddChild(_deathtimer);
        AddChild(_lifetime);
    }

    public void ForcePhysicsStateUpdate(Vector3 translate, Vector3 linear_velocity, Vector3 angular_velocity)
    {
        var rid = GetRid();
        PhysicsServer3D.BodySetState(
            rid,
            PhysicsServer3D.BodyState.Transform,
            Transform3D.Identity.Translated(translate)
        );
        PhysicsServer3D.BodySetState(
            rid,
            PhysicsServer3D.BodyState.LinearVelocity,
            linear_velocity
        );
        PhysicsServer3D.BodySetState(
            rid,
            PhysicsServer3D.BodyState.AngularVelocity,
            angular_velocity
        );
    }

    public override void _PhysicsProcess(double delta)
    { 
        // disable
        if (Freeze) return;

        // shrink effect
        if (!_deathtimer.IsStopped())
        {
            ((MeshInstance3D)GetChild(0)).Scale = _base_scale*(float)Math.Max(_deathtimer.TimeLeft/_deathtimer.WaitTime,0.1f);
            ((CollisionShape3D)GetChild(1)).Scale = _base_scale*(float)Math.Max(_deathtimer.TimeLeft/_deathtimer.WaitTime,0.1f);
        }
        else 
        {
            ((MeshInstance3D)GetChild(0)).Scale = _base_scale;
            ((CollisionShape3D)GetChild(1)).Scale = _base_scale;        
        }

        // pickup effect
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
                AudioManager.TryPlay(PickupSound,AudioBus.Coins,Player.Instance.GlobalPosition);
                Deactivate();
            }
        }
    }

    public void Activate()
    {
        MoveTowardPlayer = false;
        _lifetime.Start();
        Visible = true;
        Freeze = false;
    }

    public void Deactivate()
    {
        _lifetime.Stop();
        _deathtimer.Stop();
        ((MeshInstance3D)GetChild(0)).Scale = _base_scale;
        ((CollisionShape3D)GetChild(1)).Scale = _base_scale;
        MoveTowardPlayer = false;
        Visible = false;
        Freeze = true;
        Basis = Basis.Identity;
        CoinPool.AddToAvailableQueue(this);
    }
}
