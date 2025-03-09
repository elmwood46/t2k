using Godot;
using System;

public partial class CoinSpawner : Node3D
{
    public static readonly PackedScene CoinScene = ResourceLoader.Load<PackedScene>("res://props/treasure/coin/coin.tscn");
    public static readonly RandomNumberGenerator RNG = new();

    public double SpawnTime = 1.0;

    public int NumCoins = 10;

    private Timer _t;

    private double _spawned_coins_time;

    private double _secs_per_coin;

    private Vector3 spawn_position;

    public override void _Ready()
    {
        GlobalPosition = spawn_position;
        _secs_per_coin = SpawnTime/NumCoins;
        _spawned_coins_time = SpawnTime;
        _t = new Timer
        {
            Autostart = false,
            WaitTime = SpawnTime
        };
        _t.Timeout += () => {
            QueueFree();
        };
        AddChild(_t);
        _t.Start();
    }

    /// <summary>
    /// Creates a new CoinSpawner, sets parameters, and returns it.
    /// </summary>
    /// <param name="globalposition"></param>
    /// <param name="numCoins"></param>
    /// <param name="spawnTime"></param>
    /// <returns></returns>
    public static CoinSpawner Create(Vector3 globalposition, int numCoins = 10, double spawnTime = 1.0)
    {
        var spawner = new CoinSpawner
        {
            spawn_position = globalposition,
            NumCoins = numCoins,
            SpawnTime = spawnTime
        };
        return spawner;
    }

    public void SpawnCoin()
    {
        var ret = CoinPool.SpawnCoin((Node3D)GetTree().GetCurrentScene());
        CallDeferred(MethodName.SetupCoin,ret);
    }

    public void SetupCoin(Coin coin)
    {
        var _linvel = new Vector3(RNG.RandfRange(-2.0f,2.0f),RNG.RandfRange(10.0f,12.0f),RNG.RandfRange(-2.0f,2.0f));
        var _angvel = new Vector3(RNG.Randf()*2.0f*(float)Math.PI, RNG.Randf()*2.0f*(float)Math.PI, RNG.Randf()*2.0f*(float)Math.PI);
        coin.ForcePhysicsStateUpdate(GlobalPosition, _linvel, _angvel);
        coin.CallDeferred(nameof(coin.Activate));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_t.TimeLeft < _spawned_coins_time)
        {
            SpawnCoin();
            _spawned_coins_time -= _secs_per_coin;
        }
    }
}
