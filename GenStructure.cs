using Godot;
using System;
using System.Collections.Generic;

public static class GenStructure
{
    public static Dictionary<Vector3I, int> GenerateTotem(int height) {
        if (BlockManager.Instance == null) {
            GD.PrintErr("BlockManager not initialized");
            return null;
        }
        var ret = new Dictionary<Vector3I, int>();
        var rng = new RandomNumberGenerator();

        for (var i=0;i<height;i++) {
            var rnd = rng.RandiRange(0, 8);           
            var blocktype = rnd switch
            {
                0 or 1 => BlockManager.BlockID("MossyCobble1"),
                2 or 3 => BlockManager.BlockID("MossyCobble2"),
                4 or 5 => BlockManager.BlockID("MossyCobble3"),
                6 or 7 => BlockManager.BlockID("MossyCobble4"),
                _ => BlockManager.BlockID("GoldOre"),
            };
            if (i == height-1) blocktype = BlockManager.BlockID("Emerald");
            ret[new Vector3I(0,i,0)] = ChunkManager.PackBlockInfo(blocktype);
        }

        return ret;
    }

    public static Dictionary<Vector3I, int> GenerateTree()
    {
        if (BlockManager.Instance == null) {
            GD.PrintErr("BlockManager not initialized");
            return null;
        }
        Random rng = new();
        int _trunkHeight, _trunkRadius, _branchCount, _branchLength, _leafClusterSize, _leafcolourblock;
        _trunkHeight = rng.Next(3, 6);
        _trunkRadius = 1;
        _branchCount = rng.Next(1, 5);
        _leafClusterSize = rng.Next(3, 5);  
            _leafcolourblock = rng.Next(0, 6) switch
            {
                0 => BlockManager.BlockID("LeafRed"),
                1 => BlockManager.BlockID("LeafYellow"),
                2 => BlockManager.BlockID("LeafOrange"),
                3 => BlockManager.BlockID("LeafGreen"),
                4 => BlockManager.BlockID("LeafGreenDark"),
                _ => BlockManager.BlockID("LeafBlue"),
            };
        var _blocks = new Dictionary<Vector3I, int>();

        // Generate the trunk
        for (int y = 0; y < _trunkHeight; y++)
        {
            for (int x = -_trunkRadius; x <= _trunkRadius*2; x++)
            {
                for (int z = -_trunkRadius; z <= _trunkRadius; z++)
                {
                    if (x * x + z * z <= _trunkRadius * _trunkRadius)
                    {
                        var blocktype = BlockManager.BlockID("Trunk");
                        _blocks[new Vector3I(x, y, z)] = ChunkManager.PackBlockInfo(blocktype);
                    }
                }
            }
        }

        // Generate branches
        Random rand = new();
        for (int i = 0; i < _branchCount; i++)
        {
            int branchStartY = rand.Next(1, _trunkHeight);
            Vector3I branchStart = new(0, branchStartY, 0);

            _branchLength = rand.Next(3, 4);
            for (int j = 0; j < _branchLength; j++)
            {
                Vector3I branchPos = branchStart + new Vector3I(rand.Next(0, 2)==0 ? -1 : 1,1,rand.Next(0, 2)==0 ? -1 : 1);
                var blocktype = BlockManager.BlockID("Trunk");
                _blocks[new Vector3I(branchPos.X,branchPos.Y,branchPos.Z)] = ChunkManager.PackBlockInfo(blocktype);
            }
        }

        // Generate leaves
        for  (int z = -_leafClusterSize; z <= _leafClusterSize; z++)
        {
            for (int x = -_leafClusterSize; x <= _leafClusterSize; x++)
            {
                for (int y = _trunkHeight; y <= _trunkHeight + _leafClusterSize; y++)
                {
                    if (x * x + z * z + (y - _trunkHeight) * (y - _trunkHeight) <= _leafClusterSize * _leafClusterSize)
                    {   
                        _blocks[new Vector3I(x, y, z)] = ChunkManager.PackBlockInfo(_leafcolourblock);
                    }
                }
            }
        }

        return _blocks;
    }
}
