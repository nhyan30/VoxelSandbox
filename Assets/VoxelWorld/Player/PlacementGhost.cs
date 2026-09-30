using UnityEngine;

namespace VoxelWorld.Player
{
    /// <summary>
    /// Wireframe preview (plus a faint fill) shown at the voxel cell where the next
    /// placed cube would land. Green means the placement is legal, red means blocked.
    /// </summary>
    public sealed class PlacementGhost : MonoBehaviour
    {
        private static readonly Vector3[] Corners =
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f)
        };

        private static readonly int[] Edges =
        {
            0, 1, 1, 5, 5, 4, 4, 0, // bottom
            2, 3, 3, 7, 7, 6, 6, 2, // top
            0, 2, 1, 3, 5, 7, 4, 6  // verticals
        };

        private Material _lineMaterial;
        private Material _fillMaterial;
        private Color _validColor;
        private Color _invalidColor;

        /// <summary>Builds the ghost visuals; call once after adding the component.</summary>
        public void Initialize(Color validColor, Color invalidColor)
        {
            _validColor = validColor;
            _invalidColor = invalidColor;
            _lineMaterial = World.MaterialFactory.CreateLit(validColor);
            _fillMaterial = World.MaterialFactory.CreateTransparent(new Color(validColor.r, validColor.g, validColor.b, 0.07f));

            var lines = new Mesh { name = "GhostWireframe" };
            lines.vertices = Corners;
            lines.SetIndices(Edges, MeshTopology.Lines, 0);
            lines.RecalculateBounds();

            var linesGo = new GameObject("GhostLines");
            linesGo.transform.SetParent(transform, false);
            linesGo.AddComponent<MeshFilter>().sharedMesh = lines;
            var linesRenderer = linesGo.AddComponent<MeshRenderer>();
            linesRenderer.sharedMaterial = _lineMaterial;
            linesRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linesRenderer.receiveShadows = false;

            // Slight scale-up stops the wireframe from z-fighting with real cube faces.
            linesGo.transform.localScale = Vector3.one * 1.01f;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(fill.GetComponent<Collider>());
            fill.name = "GhostFill";
            fill.transform.SetParent(transform, false);
            // CreatePrimitive already attaches a MeshRenderer; reuse it instead of
            // AddComponent (which Unity rejects and returns null on duplicates).
            var fillRenderer = fill.GetComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = _fillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;

            gameObject.SetActive(false);
        }

        public void Show(Vector3Int voxel, bool isValid)
        {
            transform.position = new Vector3(voxel.x + 0.5f, voxel.y + 0.5f, voxel.z + 0.5f);
            _lineMaterial.color = isValid ? _validColor : _invalidColor;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}