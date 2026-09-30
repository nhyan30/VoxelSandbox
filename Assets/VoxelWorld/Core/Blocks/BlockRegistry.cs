using System.Collections.Generic;
using UnityEngine;

namespace VoxelWorld.Core.Blocks
{
    /// <summary>Static gameplay/visual data attached to one <see cref="BlockKind"/>.</summary>
    public sealed class BlockDefinition
    {
        public BlockKind Kind { get; }
        public string DisplayName { get; }
        public Color Color { get; }
        /// <summary>Seconds the player must keep mining before this cube breaks.</summary>
        public float MineSeconds { get; }
        /// <summary>Unbreakable cubes (bedrock) block mining entirely.</summary>
        public bool IsUnbreakable { get; }
        /// <summary>Whether the player may place this kind and it appears in the hotbar.</summary>
        public bool IsPlayerPlaceable { get; }

        public BlockDefinition(BlockKind kind, string displayName, Color color,
            float mineSeconds, bool isUnbreakable, bool isPlayerPlaceable)
        {
            Kind = kind;
            DisplayName = displayName;
            Color = color;
            MineSeconds = mineSeconds;
            IsUnbreakable = isUnbreakable;
            IsPlayerPlaceable = isPlayerPlaceable;
        }
    }

    /// <summary>Tuning values (mining durations) injected from the app layer.</summary>
    public sealed class BlockTuning
    {
        public float StoneMineSeconds = 1.5f;
        public float GrassMineSeconds = 0.75f;
        public float SnowMineSeconds = 0.3f;
        public float SandMineSeconds = 0.5f;
    }

    /// <summary>
    /// Immutable lookup of all block definitions. Order of <see cref="PlaceableKinds"/>
    /// defines the hotbar slot order used by the UI and the interactor.
    /// </summary>
    public sealed class BlockRegistry
    {
        private readonly Dictionary<BlockKind, BlockDefinition> _byKind;

        public IReadOnlyList<BlockDefinition> All { get; }
        public IReadOnlyList<BlockKind> PlaceableKinds { get; }

        public BlockRegistry(IReadOnlyList<BlockDefinition> definitions)
        {
            _byKind = new Dictionary<BlockKind, BlockDefinition>(definitions.Count);
            var placeable = new List<BlockKind>();
            foreach (var def in definitions)
            {
                _byKind[def.Kind] = def;
                if (def.IsPlayerPlaceable)
                {
                    placeable.Add(def.Kind);
                }
            }

            All = new List<BlockDefinition>(definitions);
            PlaceableKinds = placeable;
        }

        /// <summary>Creates the standard gray/green/white (+ sand, bedrock) palette.</summary>
        public static BlockRegistry CreateDefault(BlockTuning tuning)
        {
            return new BlockRegistry(new[]
            {
                new BlockDefinition(BlockKind.Air, "Air", new Color(0f, 0f, 0f, 0f), 0f, true, false),
                new BlockDefinition(BlockKind.Bedrock, "Bedrock", new Color(0.23f, 0.23f, 0.25f), float.PositiveInfinity, true, false),
                new BlockDefinition(BlockKind.Stone, "Stone", new Color(0.55f, 0.55f, 0.57f), tuning.StoneMineSeconds, false, true),
                new BlockDefinition(BlockKind.Grass, "Grass", new Color(0.36f, 0.63f, 0.28f), tuning.GrassMineSeconds, false, true),
                new BlockDefinition(BlockKind.Snow, "Snow", new Color(0.94f, 0.96f, 0.99f), tuning.SnowMineSeconds, false, true),
                new BlockDefinition(BlockKind.Sand, "Sand", new Color(0.86f, 0.79f, 0.52f), tuning.SandMineSeconds, false, true)
            });
        }

        public BlockDefinition Get(BlockKind kind)
        {
            return _byKind[kind];
        }

        public bool TryGet(BlockKind kind, out BlockDefinition definition)
        {
            return _byKind.TryGetValue(kind, out definition);
        }
    }
}
