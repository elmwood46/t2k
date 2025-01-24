using Godot;
using System;
using System.Collections.Generic;

public partial class ChunkManager : Node
{
    const float INVSQRT2 = 0.70710678118f;

    private static readonly Vector3 SlopedNormalNegZ = new(0, INVSQRT2, -INVSQRT2);
    private static readonly Vector3 SlopedCornerNormalNegZ = new(INVSQRT2, INVSQRT2, -INVSQRT2);


    public static int[] BatchUpdateBlockSlopeData(Vector3I chunkPosition, List<Vector3I> blockPositions, int[] chunk, bool excludeSlopes = false) {
        Dictionary<Vector3I,int> packedSlopeBlockData = new();
        foreach (var blockPosition in blockPositions) {
            var block = chunk[BlockIndex(blockPosition)];

            // dont slope empty blocks, lava, leaves,
            if (
                IsBlockEmpty(block)
                || GetBlockID(block)==BlockManager.Instance.LavaBlockId
                || (excludeSlopes&&IsBlockSloped(block)) // useful when damaging
                || GetBlockSpecies(block)==BlockSpecies.Leaves) 
            continue;

            // set whether block is flipped or not
            //var blockbelow = GetBlockNeighbour(chunkPosition, blockPosition, Vector3I.Down)!=0;
            var blockflip = false;//IsTripleBlockAbove(chunkPosition, blockPosition) && !IsBlockBelow(chunkPosition, blockPosition);

            var blockinfo = Instance.ChunkCache[chunkPosition][BlockIndex(blockPosition)];
            blockinfo = RepackSlopeData(blockinfo,GetBlockSlopeType(blockinfo),GetBlockSlopeRotationBits(blockinfo),blockflip?1:0);
            Instance.ChunkCache[chunkPosition][BlockIndex(blockPosition)] = blockinfo;
            
            var _flip = 1;
            if (blockflip) _flip = -1;

            if (!BlockIsSlopeCandidate(chunkPosition, blockPosition)) continue;

            var s = BlockPackSlopeInfo(chunkPosition, blockPosition, blockflip);
            
            if ((s&0b11) == (int)SlopeType.Corner) {//corners set the block below themselves to same type
                var neighbid = GetBlockID(GetBlockNeighbour(chunkPosition, blockPosition, Vector3I.Down * _flip));
                if (neighbid !=0 && neighbid != BlockManager.Instance.LavaBlockId) {
                    SetBlockNeighbour(chunkPosition, blockPosition, Vector3I.Down* _flip, block);
                }
            }
            packedSlopeBlockData[blockPosition] = s;
        }

        foreach (var (p, packedSlopeData) in packedSlopeBlockData) {
            var i = BlockIndex(p);
            chunk[i] = RepackSlopeData(chunk[i],packedSlopeData);
        }
        return chunk;
    }

    public static int BlockPackSlopeInfo(Vector3I chunkPosition, Vector3I blockPosition, bool blockflip) {
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

        /*if (blockflip) {
            sloperot = slopetype switch {
                1 => (sloperot+0)%4,
                2 => (sloperot+3)%4,
                3 => (sloperot+0)%4,
                _ => sloperot 
            };
        }*/

        return PackSlopeData(slopetype, sloperot, blockflip ? 1:0);
    }

    public static bool BlockIsSlopeCandidate(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return IsBlockBelow(chunkPosition, p) && (!(IsBlockAbove(chunkPosition, p)||BlockIsSurrounded(chunkPosition, p)) || BlockIsInvCorner(chunkPosition, p));
    }

    public static bool BlockIsInvCorner(Vector3I chunkPosition, Vector3I blockPosition) {
        return 
            (
                !IsBlockAbove(chunkPosition, blockPosition)
                &&BlockIsSurrounded(chunkPosition, blockPosition)
                &&BlockSingleDiagonalFree(chunkPosition, blockPosition)
            )
            || 
            (
                IsBlockAbove(chunkPosition, blockPosition)
                &&!IsDoubleBlockAbove(chunkPosition, blockPosition)
                &&BlockIsCorner(chunkPosition, blockPosition+Vector3I.Up)
                &&(BlockSingleDiagonalFree(chunkPosition, blockPosition)||BlockSingleAdjacentFree(chunkPosition, blockPosition))
            )
            || 
            (
                IsBlockAbove(chunkPosition, blockPosition)
                &&!IsDoubleBlockAbove(chunkPosition, blockPosition)
                &&BlockIsCorner(chunkPosition, blockPosition)
            );
    }

    public static int BlockInvCornerRotation(Vector3I chunkPosition, Vector3I blockPosition) {
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

    public static bool BlockIsCorner(Vector3I chunkPosition, Vector3I blockPosition) {
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

    public static int BlockCornerRotation(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) return 2;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) return 3;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) return 0;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back) && BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) return 1;
        return 0;
    }

    public static bool IsBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        var _flip = BlockIsFlipped(chunkPosition, blockPosition) ? -1 : 1;
        return BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Up*_flip);
    }

    public static bool IsDoubleBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        var _flip = BlockIsFlipped(chunkPosition, blockPosition) ? -1 : 1;
        return IsBlockAbove(chunkPosition, blockPosition)&&BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Up*_flip*2);
    }

    public static bool IsTripleBlockAbove(Vector3I chunkPosition, Vector3I blockPosition) {
        var _flip = BlockIsFlipped(chunkPosition, blockPosition) ? -1 : 1;
        return IsDoubleBlockAbove(chunkPosition, blockPosition)&&BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Up*_flip*3);
    }

    public static bool IsBlockBelow(Vector3I chunkPosition, Vector3I blockPosition) {
        var _flip = BlockIsFlipped(chunkPosition, blockPosition) ? -1 : 1;
        return BlockHasNeighbor(chunkPosition, blockPosition, Vector3I.Down*_flip);
    }

    public static bool BlockIsSurrounded(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        return BlockHasNeighbor(chunkPosition, p, Vector3I.Forward)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Back)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Left)
        && BlockHasNeighbor(chunkPosition, p, Vector3I.Right);
    }

    public static bool BlockSingleDiagonalFree(Vector3I chunkPosition, Vector3I blockPosition) {
        // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        var b1 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Right);
        var b2 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward+Vector3I.Left);
        var b3 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Right);
        var b4 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Back+Vector3I.Left);

        return (!b1 && b2 && b3 && b4) ^ (b1 && !b2 && b3 && b4) ^ (b1 && b2 && !b3 && b4) ^ (b1 && b2 && b3 && !b4);
    }

    public static bool BlockSingleAdjacentFree(Vector3I chunkPosition, Vector3I blockPosition) {
                // already assume block is inv corner
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        
        var b1 = BlockHasNeighbor(chunkPosition, p, Vector3I.Forward);
        var b2 = BlockHasNeighbor(chunkPosition, p, Vector3I.Back);
        var b3 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Left);
        var b4 =  BlockHasNeighbor(chunkPosition, p, Vector3I.Right);

        return (!b1 && b2 && b3 && b4) ^ (b1 && !b2 && b3 && b4) ^ (b1 && b2 && !b3 && b4) ^ (b1 && b2 && b3 && !b4);
    }

    public static int BlockDiagonalsCount(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        int count = 0;
        for (int x=-1;x<2;x+=2) {
                for (int z=-1;z<2;z+=2) {
                    if (BlockHasNeighbor(chunkPosition, p, new Vector3I(p.X+x, p.Y, p.Z+z))) count++;
                }
        }
        return count;
    }

    public static int BlockNeighbourCount(Vector3I chunkPosition, Vector3I blockPosition) {
        Vector3I p = new(blockPosition.X, blockPosition.Y, blockPosition.Z);
        int count = 0;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Forward)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Back)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Left)) count++;
        if (BlockHasNeighbor(chunkPosition, p, Vector3I.Right)) count++;
        return count;
    }

        public static bool BlockIsSide(Vector3I chunkPosition, Vector3I blockPosition) {
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

    public static int BlockSideRotation(Vector3I chunkPosition, Vector3I blockPosition) {
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

    public static bool BlockIsFlipped(Vector3I chunkPosition, Vector3I blockPosition) {
        var idx = BlockIndex(blockPosition);
        if (idx <0 || idx >= CSP3) return false;
        if (!Instance.ChunkCache.TryGetValue(chunkPosition, out var chunk)) return false;
        var blockinfo = chunk[BlockIndex(blockPosition)];
        return GetBlockSlopeFlip(blockinfo);
    }

    public static bool BlockHasNeighbor(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection) {
        return GetBlockNeighbour(chunkPosition, blockPosition, neighborDirection) != 0;
    }

    public static void SetBlockNeighbourIfNotEmpty(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection, int newBlockInfo)
    {
        if (!IsBlockEmpty(GetBlockNeighbour(chunkPosition,blockPosition,neighborDirection)))
        {
            SetBlockNeighbour(chunkPosition, blockPosition, neighborDirection, newBlockInfo);
        }
    }

    public static void SetBlockChunkNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection, int newBlockInfo)
    {
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);
        //p -= Vector3I.One;
        var dx = p.X > CHUNK_SIZE ? 1 : p.X < 1 ? -1 : 0;
        var dy = p.Y > CHUNK_SIZE ? 1 : p.Y < 1 ? -1 : 0;
        var dz = p.Z > CHUNK_SIZE ? 1 : p.Z < 1 ? -1 : 0;
        var delta = new Vector3I(dx, dy, dz);
        var newp = p-delta*CHUNK_SIZE;
        SetBlockNeighbour(chunkPosition+delta, newp, Vector3I.Zero, newBlockInfo);
    }

    public static void GetBlockChunkNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection, out int blockInfo)
    {
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);
        var dx = p.X > CHUNK_SIZE ? 1 : p.X < 1 ? -1 : 0;
        var dy = p.Y > CHUNK_SIZE ? 1 : p.Y < 1 ? -1 : 0;
        var dz = p.Z > CHUNK_SIZE ? 1 : p.Z < 1 ? -1 : 0;
        var delta = new Vector3I(dx, dy, dz);
        var newp = p-delta*CHUNK_SIZE;
        blockInfo = GetBlockNeighbour(chunkPosition+delta, newp, Vector3I.Zero);
    }

    public static void SetBlockNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection, int newBlockInfo)
    {
        if (!Instance.ChunkCache.TryGetValue(chunkPosition, out var chunk))
        {
            chunk = new int[CSP3*SUBCHUNKS];
        }
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);

        if (p.X < CSP && p.X >= 0
        &&  p.Y < CSP && p.Y >= 0
        &&  p.Z < CSP && p.Z >= 0)
        {
            chunk[BlockIndex(p)] = newBlockInfo;
            Instance.ChunkCache[chunkPosition] = chunk;
        }
        else
        {
            var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
            var dx = p.X >= CSP ? 1 : p.X < 0 ? -1 : 0;
            var dy = p.Y >= CSP ? 1 : p.Y < 0 ? -1 : 0;
            var dz = p.Z >= CSP ? 1 : p.Z < 0 ? -1 : 0;
            var delta = new Vector3I(dx, dy, dz);
            var newp = p-delta*CSP;
            if (!Instance.ChunkCache.TryGetValue(neighbour_chunk+delta, out var neighbour)) {
                neighbour = new int[CSP3*SUBCHUNKS];
            }

            var new_idx = BlockIndex(newp);
            neighbour[new_idx] = newBlockInfo;
            Instance.ChunkCache[neighbour_chunk+delta] = neighbour;
        }
    }

    public static int GetBlockNeighbour(Vector3I chunkPosition, Vector3I blockPosition, Vector3I neighborDirection) {
        if (!Instance.ChunkCache.TryGetValue(chunkPosition, out var chunk)) return 0;
        Vector3I p = new(blockPosition.X + neighborDirection.X, blockPosition.Y + neighborDirection.Y, blockPosition.Z + neighborDirection.Z);

        if (p.X < CSP && p.X >= 0
        &&  p.Y < CSP && p.Y >= 0
        &&  p.Z < CSP && p.Z >= 0)
        {
            var b = chunk[BlockIndex(p)];
            if (!IsBlockEmpty(b)) return b;
        }
        else
        {
            var neighbour_chunk = new Vector3I(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
            var dx = p.X >= CSP ? 1 : p.X < 0 ? -1 : 0;
            var dy = p.Y >= CSP ? 1 : p.Y < 0 ? -1 : 0;
            var dz = p.Z >= CSP ? 1 : p.Z < 0 ? -1 : 0;
            var delta = new Vector3I(dx, dy, dz);
            var newp = p-delta*CSP;
            if (!Instance.ChunkCache.TryGetValue(neighbour_chunk+delta, out var neighbour)) return 0;

            var new_idx = BlockIndex(newp);
            if (!IsBlockEmpty(neighbour[new_idx])) return neighbour[new_idx];
        }

        return 0;
    }
}
