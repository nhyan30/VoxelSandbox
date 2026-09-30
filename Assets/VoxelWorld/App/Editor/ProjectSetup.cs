#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VoxelWorld.EditorTools
{
    /// <summary>
    /// One-time editor configuration: makes sure the new Input System backend is active
    /// so the code-built input actions work from the very first Play session. The
    /// ProjectSettings.asset shipped with the project already sets 'Both'; this is a
    /// safety net in case Unity regenerates settings with defaults.
    /// </summary>
    [InitializeOnLoad]
    internal static class ProjectSetup
    {
        private const string SessionKey = "VoxelSandbox.ProjectSetupApplied";

        static ProjectSetup()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EnsureInputHandler();
        }

        private static void EnsureInputHandler()
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
    }
}
#endif
