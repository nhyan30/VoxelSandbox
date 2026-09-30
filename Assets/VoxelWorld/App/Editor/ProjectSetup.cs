#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VoxelWorld.EditorTools
{
    /// <summary>
    /// One-time editor configuration that makes the code-authored project build-safe:
    /// 1) ensures the new Input System backend is active so the code-built input actions
    ///    work from the very first Play session;
    /// 2) keeps the runtime <c>Shader.Find</c> targets in "Always Included Shaders".
    ///    The scene references no material or shader assets, so Unity's build pipeline
    ///    strips every built-in shader — <c>Shader.Find</c> then returns null inside a
    ///    player build, <c>new Material(null)</c> throws, and the game boots to a gray
    ///    screen (the editor never strips shaders, which is why Play mode works fine);
    /// 3) ships the same shaders through keepalive materials inside a Resources folder,
    ///    which Unity always includes in builds — this path works even when the
    ///    GraphicsSettings.asset file is missing or cannot be serialized.
    /// ProjectSettings.asset ships with 'Both' input handling as a default; the checks
    /// below are safety nets in case Unity regenerates settings with defaults.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectSetup
    {
        private const string InputSessionKey = "VoxelSandbox.InputSetupApplied";
        private const string ShaderSessionKey = "VoxelSandbox.ShaderSetupApplied";

        /// <summary>Shaders the code resolves only through runtime Shader.Find calls.</summary>
        internal static readonly string[] RequiredShaderNames =
        {
            "Standard",                      // MaterialFactory.CreateLit (built-in pipeline)
            "Universal Render Pipeline/Lit", // CreateLit fallback (URP projects)
            "Unlit/Color",                   // CreateLit fallback
            "Skybox/Procedural",             // GameBootstrap.SetupLighting
            "UI/Default"                     // runtime-built uGUI
        };

        static ProjectSetup()
        {
            if (!SessionState.GetBool(InputSessionKey, false))
            {
                SessionState.SetBool(InputSessionKey, true);
                EnsureInputHandler();
            }

            if (!SessionState.GetBool(ShaderSessionKey, false))
            {
                SessionState.SetBool(ShaderSessionKey, true);
                EnsureAlwaysIncludedShaders();
            }

            // Asset creation is not allowed during the import phase; delayCall defers
            // the keepalive materials until the editor has finished loading.
            EditorApplication.delayCall += EnsureBuildShaderKeepalives;
        }

        internal static void EnsureInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            var property = serialized.FindProperty("activeInputHandler");
            if (property != null && property.intValue != 2)
            {
                property.intValue = 2; // 0 = old Input Manager, 1 = Input System, 2 = both
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[VoxelSandbox] Set Active Input Handling to 'Both'. " +
                          "If input does not respond, restart the Unity editor once.");
            }
        }

        /// <summary>
        /// Adds every required shader to Graphics Settings "Always Included Shaders"
        /// (idempotent). Unity strips shaders that no scene or asset references, which
        /// would make all runtime-created materials fail inside player builds; listing
        /// them here keeps the procedural pipeline working without shipping any assets.
        /// </summary>
        internal static void EnsureAlwaysIncludedShaders()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[VoxelSandbox] Could not load GraphicsSettings.asset; " +
                                 "runtime-created materials may be stripped from builds.");
                return;
            }

            // Find the serialized object that owns the always-included shader list.
            SerializedObject serialized = null;
            SerializedProperty listProperty = null;
            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    continue;
                }

                var candidate = new SerializedObject(asset);
                var property = candidate.FindProperty("m_AlwaysIncludedShaders");
                if (property != null && property.isArray)
                {
                    serialized = candidate;
                    listProperty = property;
                    break;
                }
            }

            if (listProperty == null)
            {
                Debug.LogWarning("[VoxelSandbox] Could not locate 'm_AlwaysIncludedShaders' in GraphicsSettings; " +
                                 "add the required shaders manually via Project Settings > Graphics.");
                return;
            }

            var added = 0;
            foreach (var shaderName in RequiredShaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    continue; // Not available in this project (e.g. URP shader with built-in pipeline).
                }

                var alreadyListed = false;
                for (var i = 0; i < listProperty.arraySize; i++)
                {
                    if (listProperty.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        alreadyListed = true;
                        break;
                    }
                }

                if (alreadyListed)
                {
                    continue;
                }

                listProperty.InsertArrayElementAtIndex(listProperty.arraySize);
                listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1).objectReferenceValue = shader;
                added++;
            }

            if (added > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[VoxelSandbox] Added {added} shader(s) to 'Always Included Shaders' so " +
                          "runtime-created materials survive the build's shader stripping step.");
            }
        }

        private const string ResourcesRoot = "Assets/VoxelWorld/Resources";
        private const string KeepaliveLitPath = ResourcesRoot + "/BuildShaderKeepaliveLit.mat";
        private const string KeepaliveFadePath = ResourcesRoot + "/BuildShaderKeepaliveFade.mat";
        private const string KeepaliveSkyboxPath = ResourcesRoot + "/BuildShaderKeepaliveSkybox.mat";

        /// <summary>
        /// Pins the runtime-resolved shaders into every build through keepalive materials
        /// stored under a Resources folder: Unity unconditionally ships Resources content
        /// and never strips shaders referenced by shipped assets, independent of whether
        /// GraphicsSettings.asset exists or could be modified. The fade-variant material
        /// also ships the alpha-blend keyword variant that the placement ghost's
        /// transparent fill toggles at runtime. Idempotent; safe to call repeatedly.
        /// </summary>
        internal static void EnsureBuildShaderKeepalives()
        {
            var lit = Shader.Find("Standard");
            var skybox = Shader.Find("Skybox/Procedural");
            if (lit == null && skybox == null)
            {
                return; // Nothing to pin (non-standard pipeline setup).
            }

            if (!AssetDatabase.IsValidFolder(ResourcesRoot))
            {
                if (!AssetDatabase.IsValidFolder("Assets/VoxelWorld"))
                {
                    AssetDatabase.CreateFolder("Assets", "VoxelWorld");
                }

                AssetDatabase.CreateFolder("Assets/VoxelWorld", "Resources");
            }

            var created = 0;

            if (lit != null)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(KeepaliveLitPath) == null)
                {
                    var opaque = new Material(lit) { enableInstancing = true };
                    AssetDatabase.CreateAsset(opaque, KeepaliveLitPath);
                    created++;
                }

                if (AssetDatabase.LoadAssetAtPath<Material>(KeepaliveFadePath) == null)
                {
                    var fade = new Material(lit);
                    fade.SetInt("_Mode", 2);
                    fade.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fade.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    fade.SetInt("_ZWrite", 0);
                    fade.DisableKeyword("_ALPHATEST_ON");
                    fade.EnableKeyword("_ALPHABLEND_ON");
                    fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    fade.renderQueue = 3000;
                    fade.SetColor("_Color", new Color(1f, 1f, 1f, 0.07f));
                    AssetDatabase.CreateAsset(fade, KeepaliveFadePath);
                    created++;
                }
            }

            if (skybox != null && AssetDatabase.LoadAssetAtPath<Material>(KeepaliveSkyboxPath) == null)
            {
                AssetDatabase.CreateAsset(new Material(skybox), KeepaliveSkyboxPath);
                created++;
            }

            if (created > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[VoxelSandbox] Created " + created + " shader keepalive material(s) under " +
                          ResourcesRoot + " so runtime-created materials survive build stripping.");
            }
        }
    }

    /// <summary>
    /// Last line of defense at build time: re-checks the always-included shader list
    /// right before every player build, even if the editor-side one-time setup was
    /// skipped or reset in the current session.
    /// </summary>
    internal sealed class BuildShaderIncluder : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            ProjectSetup.EnsureAlwaysIncludedShaders();
        }
    }
}
#endif
