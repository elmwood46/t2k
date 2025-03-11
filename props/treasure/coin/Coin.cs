using Godot;
using System;
using System.Collections.Generic;

public partial class Coin : Pickup
{
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
        Bus = AudioBus.Coins;
        ImpactSounds = new();
        foreach (var stream in _coin_fall_sounds)
        {
            ImpactSounds.Add(stream);
        }
        PickupSounds = new()
        {
            PickupSound
        };

        Visible = false;
        Freeze = true;

        base._Ready();
    }

    public void Activate()
    {
        ActivatePickup = false;
        _lifetime.Start();
        Visible = true;
        Freeze = false;
    }

    override public void Deactivate()
    {
        SetCollisionLayerValue(1,false);
        SetCollisionLayerValue(2,false);
        SetCollisionLayerValue(3,false);
        SetCollisionMaskValue(1,false);
        SetCollisionMaskValue(2,false);
        SetCollisionMaskValue(3,false);
        SetCollisionMaskValue(9,false);
        _lifetime.Stop();
        _deathtimer.Stop();
        ((MeshInstance3D)GetChild(0)).Scale = _base_scale;
        ((CollisionShape3D)GetChild(1)).Scale = _base_scale;
        ActivatePickup = false;
        Visible = false;
        Freeze = true;
        CoinPool.AddToAvailableQueue(this);
    }

    override public void OnPickup()
    {
        Player.AddMoney(1);
    }
}
