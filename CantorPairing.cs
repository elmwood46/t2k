using System.Collections.Generic;
using Godot;

public partial class CantorPairing : Node
{
    private readonly HashSet<uint> _used = new();
    private readonly object _lock = new();

    public static CantorPairing Instance { get; } = new();

    private static uint MapToNatural(int n) => n >= 0 ? (uint)(2 * n) : (uint)(-2 * n - 1);

    private static uint Pair2D(uint a, uint b) => ((a + b) * (a + b + 1)) / 2 + b;

    private static uint Pair3D(uint a, uint b, uint c) => Pair2D(Pair2D(a, b), c);

    public static bool Contains(Vector3I coords)
    {
        var paired = Pair3D(MapToNatural(coords.X), MapToNatural(coords.Y), MapToNatural(coords.Z));
        lock (Instance._lock)
        {
            return Instance._used.Contains(paired);
        }
    }

    public static void Add(Vector3I coords)
    {
        var paired = Pair3D(MapToNatural(coords.X), MapToNatural(coords.Y), MapToNatural(coords.Z));
        lock (Instance._lock)
        {
            Instance._used.Add(paired);
        }
    }
}