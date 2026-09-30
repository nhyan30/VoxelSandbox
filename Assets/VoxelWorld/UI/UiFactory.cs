using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoxelWorld.UI
{
    /// <summary>
    /// Small helpers for building uGUI widgets from code. The whole UI is code-authored
    /// (no scene/prefab assets) and deliberately uses the classic uGUI Text with the
    /// built-in runtime font so no TextMeshPro essentials import is required.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>Unity 6 renamed the built-in font to LegacyRuntime; older names are tried as a fallback.</summary>
        public static Font DefaultFont()
        {
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var font = Resources.GetBuiltinResource<Font>(name);
                    if (font != null)
                    {
                        return font;
                    }
                }
                catch
                {
                    // try the next candidate
                }
            }

            return null;
        }

        public static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Creates the event system once, matching the active input backend.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform SetAnchor(RectTransform t, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            t.anchorMin = anchorMin;
            t.anchorMax = anchorMax;
            t.pivot = pivot;
            t.anchoredPosition = anchoredPosition;
            t.sizeDelta = sizeDelta;
            return t;
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, size);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.17f, 0.2f, 0.95f);

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.3f, 0.36f);
            colors.pressedColor = new Color(0.12f, 0.13f, 0.16f);
            button.colors = colors;

            var text = CreateText(go.transform, "Label", label, 20, TextAnchor.MiddleCenter, Color.white);
            SetAnchor(text.rectTransform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            return button;
        }
    }
}
