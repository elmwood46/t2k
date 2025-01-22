using Godot;
using System;

public partial class ChunkManager : Node
{
    const float INVSQRT2 = 0.70710678118f;

    private static readonly Vector3 SlopedNormalNegZ = new(0, INVSQRT2, -INVSQRT2);
    private static readonly Vector3 SlopedCornerNormalNegZ = new(INVSQRT2, INVSQRT2, -INVSQRT2);

    public int BlockPackSlopeInfo(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        Vector3I chunkpos = new(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);

        var slopetype = 0;
        var sloperot = 0;

        // these functions assume that the block is a slope candidate already (not empty, surrounded, has a block below and no block above)


        if (BlockIsCorner(chunkpos, p)) {
            slopetype = 2;
            sloperot = BlockCornerRotation(chunkpos, p);
        }
        if (BlockIsSide(chunkpos, p)) {
            slopetype = 1;
            sloperot = BlockSideRotation(chunkpos, p);
        }
        if (BlockIsInvCorner(chunkpos, p)) {
            slopetype = 3;
            sloperot = BlockInvCornerRotation(chunkpos, p);
        }

        return PackSlopeData(slopetype, sloperot);
    }

    public bool BlockIsSlopeCandidate(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return IsBlockBelow(chunkPosition, p) && (!(IsBlockAbove(chunkPosition, p)||BlockIsSurrounded(chunkPosition, p)) || BlockIsInvCorner(chunkPosition, p));
    }

        public bool BlockIsInvCorner(Vector3I chunkPosition, Vector3I blockPosition) {
            return 
                (
                    !IsBlockAbove(chunkPosition, blockPosition)
                    &&BlockIsSurrounded(chunkPosition, blockPosition)
                    &&BlockSingleDiagonalFree(chunkPosition, blockPosition)
                )
                || 
                (
                    IsBlockAbove(chunkPosition, blockPosition)
                    &&BlockIsCorner(chunkPosition, blockPosition+Vector3I.Up)
                    &&!IsDoubleBlockAbove(chunkPosition, blockPosition)
                    &&(BlockSingleDiagonalFree(chunkPosition, blockPosition)||BlockSingleAdjacentFree(chunkPosition, blockPosition))
                )
                || 
                (
                    IsBlockAbove(chunkPosition, blockPosition)
                    &&!IsDoubleBlockAbove(chunkPosition, blockPosition)
                    &&BlockIsCorner(chunkPosition, blockPosition)
                );
        }

    public int BlockInvCornerRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        if 
        (
            (
                !IsBlockAbove(chunkPosition, blockPosition)
                &&BlockIsSurrounded(chunkPosition, blockPosition)
                &&BlockSingleDiagonalFree(chunkPosition, blockPosition)
            )
            || 
            (
                IsBlockAbove(chunkPosition, blockPosition)
                &&BlockIsCorner(chunkPosition, blockPosition+Vector3I.Up)
                &&!IsDoubleBlockAbove(chunkPosition, blockPosition)
                &&BlockSingleDiagonalFree(chunkPosition, blockPosition)
            )
        )
        {
            var b1 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Right);
            var b2 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Left);
            var b3 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Right);
            var b4 = !BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Left);

            if (b1) return 0;
            if (b2) return 1;
            if (b4) return 2;
            if (b3) return 3;
            return 0;
        }
        else return BlockCornerRotation(chunkPosition, blockPosition+Vector3I.Up);
    }

    public bool BlockIsCorner(Vector3I chunkPosition, Vector3I blockPosition) {
        // these functions assume that the block is a slope candidate already (not empty, surrounded, has a block below and no block above)
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);


        if (
                ! (
                    (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Forward) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Right))
                    ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Forward) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Left))
                    ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Back) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Right))
                    ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Back) && !IsTripleBlockAbove(chunkPosition, p+ Vector3I.Left))
                )
            ) return false;


        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Forward);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Right);
            if (!IsBlockSloped(b1) && !IsBlockSloped(b2)) return true;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Forward);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Left);
            if (!IsBlockSloped(b1) && !IsBlockSloped(b2)) return true;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Back);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Right);
            if (!IsBlockSloped(b1) && !IsBlockSloped(b2)) return true;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) {
            var b1 = GetBlockNeighbour(chunkPosition, p, Vector3I.Back);
            var b2 = GetBlockNeighbour(chunkPosition, p, Vector3I.Left);
            if (!IsBlockSloped(b1) && !IsBlockSloped(b2)) return true;
        }

        return false;
    }

    public int BlockCornerRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) return 2;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) return 3;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) return 0;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) return 1;
        return 0;
    }

    public bool IsBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        return BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Up);
    }

    public bool IsDoubleBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        return IsBlockAbove(chunkPosition, blockPosition)&&IsBlockAbove(chunkPosition, blockPosition+Vector3I.Up);
    }

    public bool IsTripleBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        return IsBlockAbove(chunkPosition, blockPosition)&&IsBlockAbove(chunkPosition, blockPosition+Vector3I.Up)&&IsBlockAbove(chunkPosition, blockPosition+2*Vector3I.Up);
    }

    public bool IsBlockBelow(Vector3I chunkPosition, Vector3I blockPosition) {
        return BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Down);
    }

    public bool BlockIsSurrounded(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return BlockHasNeighbor(chunkPosition, p, Vector3I.Forward)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Back)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Right);
    }

    public bool BlockSingleDiagonalFree(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        var b1 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Right);
        var b2 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Left);
        var b3 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Right);
        var b4 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Left);

        return (!b1 && b2 && b3 && b4) ^ (b1 && !b2 && b3 && b4) ^ (b1 && b2 && !b3 && b4) ^ (b1 && b2 && b3 && !b4);
    }

    public bool BlockSingleAdjacentFree(Vector3I chunkPosition, Vector3I blockPosition) {
                // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        var b1 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward);
        var b2 = BlockHasNeighbor(chunkPosition, p, Vector3I.Back);
        var b3 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Left);
        var b4 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Right);

        return (!b1 && b2 && b3 && b4) ^ (b1 && !b2 && b3 && b4) ^ (b1 && b2 && !b3 && b4) ^ (b1 && b2 && b3 && !b4);
    }

    public int BlockDiagonalsCount(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        int count = 0;
        for (int x=-1;x<2;x+=2) {
                for (int z=-1;z<2;z+=2) {
                    if (BlockHasNeighbor(chunkPosition, p, new Vector3I(p.X+x, p.Y, p.Z+z))) count++;
                }
        }
        return count;
    }

    public int BlockNeighbourCount(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        int count = 0;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) count++;
        return count;
    }

        public bool BlockIsSide(Vector3I chunkPosition, Vector3I blockPosition) {
        // these functions assume that the block is a slope candidate already (not empty, surrounded, has a block below and no block above)
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (!(BlockNeighbourCount(chunkPosition, p) == 3 || BlockNeighbourCount(chunkPosition, p) == 1)) return false;

        if (BlockNeighbourCount(chunkPosition, p) == 1) {
            return
                (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Forward))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Back))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Left))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Right));
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Back))
        {
            return
                (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Left))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Right));

        }
        else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left))
        {
            return
                (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Forward))
                ^ (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Back));
        }

        return false;
    }

    public int BlockSideRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is slope
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (BlockNeighbourCount(chunkPosition, p) == 1) {
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Forward)) return 2;
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Back)) return 0;
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Left)) return 3;
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Right)) return 1;
        }

        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Back))
        {
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Right)) return 1;
            else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Left)) return 3;  
        }
        else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left))
        {
            if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Forward)) return 2;
            else if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && !IsTripleBlockAbove(chunkPosition, p+Vector3I.Back)) return 0;
        }
    

        return 0;
    }

    public bool BlockHasNeighbor(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection) {
        return GetBlockNeighbour(chunkPosition, blockPosition, neighborDirection) != 0;
    }

    public void SetBlockNeighbourIfNotEmpty(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection, int newBlockInfo)
    {
        if (!IsBlockEmpty(GetBlockNeighbour(chunkPosition,blockPosition,neighborDirection)))
        {
            SetBlockNeighbour(chunkPosition, blockPosition, neighborDirection, newBlockInfo);
        }
    }

    public void SetBlockNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection, int newBlockInfo)
    {
        var chunk = ChunkCache[chunkPosition];
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);

        if (p.X < CHUNK_SIZE && p.X >= 0
        &&  p.Y < CHUNK_SIZE && p.Y >= 0
        &&  p.Z < CHUNK_SIZE && p.Z >= 0)
        {
            chunk[p.X + p.Y*CHUNKSQ + p.Z*CHUNK_SIZE] = newBlockInfo;
        }
        else
        {
            var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
            var dx = p.X > CHUNK_SIZE-1 ? 1 : p.X < 0 ? -1 : 0;
            var dy = p.Y > CHUNK_SIZE-1 ? 1 : p.Y < 0 ? -1 : 0;
            var dz = p.Z > CHUNK_SIZE-1 ? 1 : p.Z < 0 ? -1 : 0;
            var delta = new Vector3I(dx, dy, dz);
            var newp = p-delta*CHUNK_SIZE;
            if (!ChunkCache.TryGetValue(neighbour_chunk+delta, out var neighbour)) {
                neighbour = new int[CHUNKSQ*CHUNK_SIZE*SUBCHUNKS];
                ChunkCache[neighbour_chunk+delta] = neighbour;
            }

            var new_idx = newp.X + newp.Y*CHUNKSQ + newp.Z*CHUNK_SIZE;
            neighbour[new_idx] = newBlockInfo;
        }
    }

    public int GetBlockNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection) {
        var chunk = ChunkCache[chunkPosition];
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);

        if (p.X < CHUNK_SIZE && p.X >= 0
        &&  p.Y < CHUNK_SIZE && p.Y >= 0
        &&  p.Z < CHUNK_SIZE && p.Z >= 0)
        {
            var b = chunk[p.X + p.Y*CHUNKSQ + p.Z*CHUNK_SIZE];
            if (!IsBlockEmpty(b)) return b;
        }
        else
        {
            var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
            var dx = p.X > CHUNK_SIZE-1 ? 1 : p.X < 0 ? -1 : 0;
            var dy = p.Y > CHUNK_SIZE-1 ? 1 : p.Y < 0 ? -1 : 0;
            var dz = p.Z > CHUNK_SIZE-1 ? 1 : p.Z < 0 ? -1 : 0;
            var delta = new Vector3I(dx, dy, dz);
            var newp = p-delta*CHUNK_SIZE;
            if (!ChunkCache.TryGetValue(neighbour_chunk+delta, out var neighbour)) {
                neighbour = new int[CHUNKSQ*CHUNK_SIZE*SUBCHUNKS];
                ChunkCache[neighbour_chunk+delta] = neighbour;
            }

            var new_idx = newp.X + newp.Y*CHUNKSQ + newp.Z*CHUNK_SIZE;
            if (!IsBlockEmpty(neighbour[new_idx])) return neighbour[new_idx];
        }

        return 0;
    }
}
