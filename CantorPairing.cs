using System;
using System.Collections.Generic;
using Godot;

public class CantorPairing
{
    private HashSet<long> _used = new();

    public static CantorPairing Instance { get; } = new();

    // Map integers to natural numbers
    public static long MapToNatural(long n)
    {
        return n >= 0 ? 2L * n : -2L * n - 1;
    }

    // 2D Cantor pairing function
    public static long Pair2D(long a, long b)
    {
        return ((a + b) * (a + b + 1)) / 2 + b;
    }

    // 3D Cantor pairing function
    public static long Pair3D(long a, long b, long c)
    {
        long p1 = Pair2D(a, b); // Pair the first two numbers
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