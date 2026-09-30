using System;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelWorld.UI
{
    /// <summary>
    /// Pause menu: dim overlay with Resume/Save/Load/Controls/Quit plus an inline
    /// controls reference screen (the project ships no README, the menu *is* the manual).
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        public event Action ResumeRequested;
        public event Action SaveRequested;
        public event Action LoadRequested;
        public event Action QuitRequested;

        private GameObject _mainGroup;
        private GameObject _controlsGroup;
        private Text _status;

        public static PauseMenuView Build(Canvas canvas)
        {
            var go = new GameObject("PauseMenu");
            go.transform.SetParent(canvas.transform, false);
            var view = go.AddComponent<PauseMenuView>();

            var dim = UiFactory.CreatePanel(go.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            UiFactory.SetAnchor(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);

            var panel = UiFactory.CreatePanel(go.transform, "Panel", new Color(0.09f, 0.1f, 0.13f, 0.94f));
            UiFactory.SetAnchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.one * 0.5f, Vector2.zero, new Vector2(480f, 520f));

            var title = UiFactory.CreateText(panel.transform, "Title", "PAUSED", 34, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(400f, 48f));

            view.BuildMainGroup(panel.transform);
            view.BuildControlsGroup(panel.transform);
            go.SetActive(false);
            return view;
        }

        private void BuildMainGroup(Transform panel)
        {
            _mainGroup = new GameObject("Main");
            _mainGroup.transform.SetParent(panel, false);

            var buttons = new[] { "Resume", "Save World", "Load World", "Controls", "Quit" };
            for (var i = 0; i < buttons.Length; i++)
            {
                var label = buttons[i];
                var button = UiFactory.CreateButton(_mainGroup.transform, $"Button_{label}", label,
                    new Vector2(0f, 84f - i * 58f), new Vector2(340f, 46f));
                switch (i)
                {
                    case 0: button.onClick.AddListener(() => ResumeRequested?.Invoke()); break;
                    case 1: button.onClick.AddListener(() => SaveRequested?.Invoke()); break;
                    case 2: button.onClick.AddListener(() => LoadRequested?.Invoke()); break;
                    case 3: button.onClick.AddListener(ToggleControls); break;
                    default: button.onClick.AddListener(() => QuitRequested?.Invoke()); break;
                }
            }

            _status = UiFactory.CreateText(_mainGroup.transform, "Status", string.Empty, 16, TextAnchor.MiddleCenter,
                new Color(0.7f, 0.85f, 0.6f));
            UiFactory.SetAnchor(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(440f, 26f));
        }

        private void BuildControlsGroup(Transform panel)
        {
            _controlsGroup = new GameObject("Controls");
            _controlsGroup.transform.SetParent(panel, false);

            const string controlsText =
                "CONTROLS\n\n" +
                "WASD — move        Mouse — look\n" +
                "Space — jump       Left Shift — sprint\n\n" +
                "Hold Left Mouse — mine the targeted cube\n" +
                "Right Mouse — place the selected cube\n\n" +
                "1-4 / Mouse wheel — select cube type\n\n" +
                "Esc — pause menu\n" +
                "F5 — save world    F9 — load world\n\n" +
                "Gray stone digs slowest, green grass is normal,\n" +
                "white snow is fastest. Bedrock cannot be mined.\n" +
                "Mined cubes are stored in your hotbar.";

            var text = UiFactory.CreateText(_controlsGroup.transform, "ControlsText", controlsText, 17,
                TextAnchor.UpperCenter, new Color(0.92f, 0.94f, 0.98f));
            UiFactory.SetAnchor(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.one * 0.5f, new Vector2(0f, -20f), new Vector2(440f, 400f));

            var back = UiFactory.CreateButton(_controlsGroup.transform, "Button_Back", "Back",
                new Vector2(0f, -196f), new Vector2(200f, 42f));
            back.onClick.AddListener(ToggleControls);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
            {
                _controlsGroup.SetActive(false);
                _mainGroup.SetActive(true);
            }
        }

        public void ToggleControls()
        {
            var showControls = !_controlsGroup.activeSelf;
            _controlsGroup.SetActive(showControls);
            _mainGroup.SetActive(!showControls);
        }

        public void SetStatus(string message)
        {
            _status.text = message;
        }
    }
}
