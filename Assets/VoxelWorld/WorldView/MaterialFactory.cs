using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelWorld.World
{
    /// <summary>
    /// Creates materials at runtime instead of shipping .mat assets, keeping the whole
    /// project code-authored. Falls back across render pipelines so the project also
    /// renders (without pink) if someone drops it into a URP/HDRP project.
    /// </summary>
    public static class MaterialFactory
    {
        public static Material CreateLit(Color color)
        {
            var shader = Shader.Find("Standard")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Unlit/Color");

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.enableInstancing = true;
            return material;
        }

        public static Material CreateTransparent(Color color)
        {
            var material = CreateLit(color);
            if (material.HasProperty("_Mode"))
            {
                // Standard shader fade mode (transparent, no z-write).
                material.SetInt("_Mode", 2);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }

            var faded = color;
            faded.a = Mathf.Clamp01(faded.a);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", faded);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", faded);
            }

            return material;
        }
    }
}
