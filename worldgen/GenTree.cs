using Godot;
using System;
using System.Collections.Generic;

public partial class GenTree : Node3D
{
    private int _trunkHeight, _trunkRadius, _branchCount, _branchLength, _leafClusterSize;
    public Dictionary<(int,int,int), Block> Blocks {get; private set;}

    public GenTree()
    {
        Random rng = new Random();
        _trunkHeight = rng.Next(3, 6);
        _trunkRadius = 1;//rng.Next(1, 1);
        _branchCount = rng.Next(1, 5);
        _branchLength = rng.Next(3, 4);
        _leafClusterSize = rng.Next(3, 5);
        Blocks = GenerateTree();
    }

    private Dictionary<(int,int,int), Block> GenerateTree()
    {
        Dictionary<(int,int,int), Block>  _blocks = new Dictionary<(int,int,int), Block> ();

        // Generate the trunk
        for (int y = 0; y < _trunkHeight; y++)
        {
            for (int x = -_trunkRadius; x <= _trunkRadius; x++)
            {
                for (int z = -_trunkRadius; z <= _trunkRadius; z++)
                {
                    if (x * x + z * z <= _trunkRadius * _trunkRadius)
                    {
                        _blocks[(x, y, z)] = BlockManager.Instance.Trunk;
                    }
                }
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
                        _blocks[(x, y, z)] = BlockManager.Instance.Leaves;
                    }
                }
            }
        }

        // Generate branches
        Random rand = new Random();
        for (int i = 0; i < _branchCount; i++)
        {
            int branchStartY = rand.Next(1, _trunkHeight);
            Vector3I branchStart = new Vector3I(0, branchStartY, 0);

            _branchLength = rand.Next(3, 4);
            for (int j = 0; j < _branchLength; j++)
            {
                Vector3I branchPos = branchStart + new Vector3I(rand.Next(0, 2)==0 ? -1 : 1,1,rand.Next(0, 2)==0 ? -1 : 1);
                _blocks[(branchPos.X,branchPos.Y,branchPos.Z)] = BlockManager.Instance.Trunk;
            }
        }

        return _blocks;
    }
}
