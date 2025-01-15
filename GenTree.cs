using Godot;
using System;
using System.Collections.Generic;

public partial class GenTree : Node3D
{
    private int _trunkHeight, _trunkRadius, _branchCount, _branchLength, _leafClusterSize, _leafcolourblock;
    public Dictionary<Vector3I, int> Blocks {get; private set;}

    public static readonly Random rng = new();

    public GenTree()
    {
        _trunkHeight = rng.Next(3, 6);
        _trunkRadius = 1;//rng.Next(1, 1);
        _branchCount = rng.Next(1, 5);
        _branchLength = rng.Next(3, 4);
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
        Blocks = GenerateTree();
    }

    private Dictionary<Vector3I, int> GenerateTree()
    {
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
                        _blocks[new Vector3I(x, y, z)] = BlockManager.InitBlockInfo(blocktype);
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
                _blocks[new Vector3I(branchPos.X,branchPos.Y,branchPos.Z)] = BlockManager.InitBlockInfo(blocktype);
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
                        _blocks[new Vector3I(x, y, z)] = BlockManager.InitBlockInfo(_leafcolourblock);
                    }
                }
            }
        }

        return _blocks;
    }
}
