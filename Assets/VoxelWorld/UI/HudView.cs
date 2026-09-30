using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoxelWorld.Core.Blocks;

namespace VoxelWorld.UI
{
    /// <summary>
    /// In-game HUD: crosshair, mining progress bar, hotbar (one slot per placeable block
    /// kind with live counts), toast messages and a permanent controls hint. Pure view —
    /// <see cref="GameUiController"/> feeds it.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private readonly List<Image> _slotBackgrounds = new List<Image>();
        private readonly List<Outline> _slotOutlines = new List<Outline>();
        private readonly List<Text> _slotCounts = new List<Text>();
        private readonly Dictionary<BlockKind, int> _kindToSlot = new Dictionary<BlockKind, int>();

        private Image _miningFill;
        private GameObject _miningGroup;
        private Text _toast;
        private float _toastShownAt;
        private const float ToastHoldSeconds = 1.6f;
        private const float ToastFadeSeconds = 0.7f;

        public int SlotCount => _slotBackgrounds.Count;

        /// <summary>Builds the whole HUD under a canvas.</summary>
        public static HudView Build(Canvas canvas, BlockRegistry registry)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(canvas.transform, false);
            var view = go.AddComponent<HudView>();

            BuildCrosshair(go.transform);
            view.BuildMiningBar(go.transform);
            view.BuildHotbar(go.transform, registry);
            view.BuildToastsAndHint(go.transform);
            return view;
        }

        private static void BuildCrosshair(Transform parent)
        {
            var color = new Color(1f, 1f, 1f, 0.75f);
            var horizontal = UiFactory.CreatePanel(parent, "CrosshairH", color);
            UiFactory.SetAnchor(horizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.one * 0.5f, Vector2.zero, new Vector2(16f, 2f));
            var vertical = UiFactory.CreatePanel(parent, "CrosshairV", color);
            UiFactory.SetAnchor(vertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.one * 0.5f, Vector2.zero, new Vector2(2f, 16f));
        }

        private void BuildMiningBar(Transform parent)
        {
            _miningGroup = new GameObject("MiningProgress");
            _miningGroup.transform.SetParent(parent, false);

            var background = UiFactory.CreatePanel(_miningGroup.transform, "Background", new Color(0f, 0f, 0f, 0.45f));
            UiFactory.SetAnchor(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.one * 0.5f, new Vector2(0f, -44f), new Vector2(200f, 10f));

            _miningFill = UiFactory.CreatePanel(background.transform, "Fill", new Color(1f, 0.85f, 0.3f, 0.95f));
            UiFactory.SetAnchor(_miningFill.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f),
                new Vector2(2f, 0f), new Vector2(196f, 6f));
            _miningFill.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            _miningGroup.SetActive(false);
        }

        private void BuildHotbar(Transform parent, BlockRegistry registry)
        {
            var slots = registry.PlaceableKinds;
            const float slotSize = 72f;
            const float gap = 8f;
            var width = slots.Count * slotSize + (slots.Count - 1) * gap;

            var container = UiFactory.CreatePanel(parent, "Hotbar", new Color(0f, 0f, 0f, 0f));
            UiFactory.SetAnchor(container.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(width, slotSize + 12f));

            for (var i = 0; i < slots.Count; i++)
            {
                var kind = slots[i];
                _kindToSlot[kind] = i;

                var slot = UiFactory.CreatePanel(container.transform, $"Slot_{kind}", new Color(0.06f, 0.06f, 0.08f, 0.7f));
                UiFactory.SetAnchor(slot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.one * 0.5f, new Vector2(i * (slotSize + gap) - width * 0.5f + slotSize * 0.5f, 0f),
                    new Vector2(slotSize, slotSize));

                var outline = slot.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0f);
                outline.effectDistance = new Vector2(2f, -2f);

                var swatch = UiFactory.CreatePanel(slot.transform, "Swatch", registry.Get(kind).Color);
                UiFactory.SetAnchor(swatch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.one * 0.5f, new Vector2(0f, 4f), new Vector2(40f, 40f));

                var count = UiFactory.CreateText(slot.transform, "Count", "x0", 15, TextAnchor.MiddleRight,
                    new Color(1f, 1f, 1f, 0.9f));
                UiFactory.SetAnchor(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(56f, 18f));

                var key = UiFactory.CreateText(slot.transform, "Key", (i + 1).ToString(), 14, TextAnchor.UpperLeft,
                    new Color(1f, 1f, 1f, 0.55f));
                UiFactory.SetAnchor(key.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(4f, -2f), new Vector2(24f, 18f));

                _slotBackgrounds.Add(slot);
                _slotOutlines.Add(outline);
                _slotCounts.Add(count);
            }
        }

        private void BuildToastsAndHint(Transform parent)
        {
            _toast = UiFactory.CreateText(parent, "Toast", string.Empty, 22, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0f));
            UiFactory.SetAnchor(_toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.one * 0.5f, new Vector2(0f, 110f), new Vector2(900f, 40f));

            var hint = UiFactory.CreateText(parent, "Hint",
                "WASD move · Mouse look · Hold LMB mine · RMB place · 1-4 / wheel select · Esc menu · F5 save · F9 load",
                14, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));
            UiFactory.SetAnchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(1200f, 22f));
        }

        private void Update()
        {
            if (_toast == null || _toast.color.a <= 0f)
            {
                return;
            }

            var elapsed = Time.unscaledTime - _toastShownAt;
            var alpha = elapsed <= ToastHoldSeconds
                ? 1f
                : Mathf.Clamp01(1f - (elapsed - ToastHoldSeconds) / ToastFadeSeconds);
            var color = _toast.color;
            color.a = alpha;
            _toast.color = color;
        }

        public void SetMiningProgress(float normalized, bool visible)
        {
            if (_miningGroup == null)
            {
                return;
            }

            _miningGroup.SetActive(visible && normalized > 0f);
            _miningFill.rectTransform.localScale = new Vector3(normalized, 1f, 1f);
        }

        public void SetSelectedSlot(int slot)
        {
            for (var i = 0; i < _slotOutlines.Count; i++)
            {
                _slotOutlines[i].effectColor = new Color(1f, 1f, 1f, i == slot ? 0.9f : 0f);
                _slotBackgrounds[i].color = i == slot
                    ? new Color(0.16f, 0.18f, 0.22f, 0.85f)
                    : new Color(0.06f, 0.06f, 0.08f, 0.7f);
            }
        }

        public void SetCount(BlockKind kind, int count)
        {
            if (_kindToSlot.TryGetValue(kind, out var slot) && slot < _slotCounts.Count)
            {
                _slotCounts[slot].text = $"x{count}";
            }
        }

        public void PostToast(string message)
        {
            _toast.text = message;
            var color = _toast.color;
            color.a = 1f;
            _toast.color = color;
            _toastShownAt = Time.unscaledTime;
        }
    }
}
