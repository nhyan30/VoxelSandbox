using UnityEngine;
using VoxelWorld.Core.Blocks;

namespace VoxelWorld.World
{
    /// <summary>
    /// One rendered cube. Views are cheap, pooled GameObjects sharing a single unit
    /// cube mesh and one material per block kind — only *exposed* cells ever get a view
    /// (see <see cref="ChunkView"/>), buried voxels stay pure data.
    /// </summary>
    public sealed class CubeView : MonoBehaviour
    {
        public BlockKind Kind { get; private set; }

        private MeshRenderer _renderer;

        /// <summary>Creates the pooled template cube (shared mesh + box collider).</summary>
        public static CubeView Create(Transform poolRoot, int blockLayer, bool castShadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cube";
            go.layer = Mathf.Max(0, blockLayer);
            go.SetActive(false);

            var view = go.AddComponent<CubeView>();
            view._renderer = go.GetComponent<MeshRenderer>();
            view._renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            view._renderer.receiveShadows = castShadows;

            if (poolRoot != null)
            {
                go.transform.SetParent(poolRoot, false);
            }

            return view;
        }

        /// <summary>Assigns kind/material and makes the view visible.</summary>
        public void Bind(BlockKind kind, Material material)
        {
            Kind = kind;
            _renderer.sharedMaterial = material;
            gameObject.name = $"Cube_{kind}";
            gameObject.SetActive(true);
        }

        /// <summary>Snap the view to a voxel cell (cell corner at <paramref name="blockPos"/>).</summary>
        public void PlaceAt(Vector3Int blockPos)
        {
            transform.position = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
        }
    }
}
