using VoxelWorld.Core.Blocks;

namespace VoxelWorld.Core.Generation
{
    /// <summary>Turns world coordinates into block kinds. Implementations must be deterministic per seed.</summary>
    public interface ITerrainGenerator
    {
        /// <summary>Topmost solid block y of a column (before any player edits).</summary>
        int HeightAt(int x, int z);

        /// <summary>Block kind generated at an absolute world position.</summary>
        BlockKind GenerateBlock(int x, int y, int z);
    }
}
