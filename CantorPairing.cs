using System;
using Godot;

public static class CantorPairing
{
    private HashSet<long> _used = new HashSet<long>();

    public static CantorPairing Instance { get; } = new CantorPairing();

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

    // Decode 2D Cantor pairing function
    public static (long a, long b) Decode2D(long z)
    {
        long w = (long)Math.Floor((Math.Sqrt(8 * z + 1) - 1) / 2);
        long t = (w * (w + 1)) / 2;
        long b = z - t;
        long a = w - b;
        return (a, b);
    }

    // Decode 3D Cantor pairing function
    public static (long a, long b, long c) Decode3D(long z)
    {
        // Decode the outermost pairing
        (long p1, long c) = Decode2D(z);
        
        // Decode the inner pairing
        (long a, long b) = Decode2D(p1);

        return (a, b, c);
    }

    public static void ContainsCoords(Vector3I coords) {
        var paired = Pair3D(coords.X, coords.Y, coords.Z);
    }



    // Example usage
    public static void Main()
    {
        long a = 5, b = 7, c = 3;

        // Encode
        long paired = Pair3D(a, b, c);
        Console.WriteLine($"Paired: {paired}");

        // Decode
        var decoded = Decode3D(paired);
        Console.WriteLine($"Decoded: ({decoded.a}, {decoded.b}, {decoded.c})");
    }
}
