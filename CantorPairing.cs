using System.Collections.Generic;
using Godot;

public class CantorPairing
{
    private HashSet<uint> _used = new();

    public static CantorPairing Instance { get; } = new();

    // Map integers to natural numbers
    public static uint MapToNatural(int n)
    {
        return n >= 0 ? (uint)(2 * n) : (uint)(-2 * n - 1);
    }

    // 2D Cantor pairing function
    public static uint Pair2D(uint a, uint b)
    {
        return ((a + b) * (a + b + 1)) / 2 + b;
    }

    // 3D Cantor pairing function
    public static uint Pair3D(uint a, uint b, uint c)
    {
        var p1 = Pair2D(a, b); // Pair the first two numbers
        return Pair2D(p1, c);  // Pair the result with the third number
    }

    public static bool Contains(Vector3I coords) {
        var paired = Pair3D(MapToNatural(coords.X), MapToNatural(coords.Y), MapToNatural(coords.Z));
        return Instance._used.Contains(paired);
    }

    public static void Add(Vector3I coords) {
        var paired = Pair3D(MapToNatural(coords.X), MapToNatural(coords.Y), MapToNatural(coords.Z));
        Instance._used.Add(paired);
    }
}