namespace VoxelWorld.Core.Blocks
{
    /// <summary>
    /// Every discrete substance a voxel cell can contain. <see cref="Air"/> is the
    /// implicit "empty" state of the grid; all other kinds are solid cubes.
    /// </summary>
    public enum BlockKind : byte
    {
        Air = 0,
        Bedrock = 1,
        Stone = 2,
        Grass = 3,
        Snow = 4,
        Sand = 5
    }
}
