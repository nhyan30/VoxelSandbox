using System;
using UnityEngine;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Inventory;
using VoxelWorld.Player;

namespace VoxelWorld.UI
{
    /// <summary>Menu callbacks the app layer provides (composition stays in the bootstrap).</summary>
    public sealed class MenuCallbacks
    {
        public Action Resume;
        public Action Save;
        public Action Load;
        public Action Quit;
    }

    /// <summary>
    /// Binds gameplay state to the HUD and menu views: inventory counts, hotbar
    /// selection, mining progress, toasts, and the menu button wiring.
    /// </summary>
    public sealed class GameUiController : MonoBehaviour
    {
        private HudView _hud;
        private PauseMenuView _menu;
        private IInventory _inventory;
        private BlockRegistry _registry;
        private PlayerInteractor _interactor;

        public static GameUiController Create(Canvas canvas, HudView hud, PauseMenuView menu,
            IInventory inventory, BlockRegistry registry, PlayerInteractor interactor, MenuCallbacks callbacks)
        {
            var go = new GameObject("UIController");
            go.transform.SetParent(canvas.transform, false);
            var controller = go.AddComponent<GameUiController>();
            controller._hud = hud;
            controller._menu = menu;
            controller._inventory = inventory;
            controller._registry = registry;
            controller._interactor = interactor;

            controller._menu.ResumeRequested += () => callbacks.Resume();
            controller._menu.SaveRequested += () => callbacks.Save();
            controller._menu.LoadRequested += () => callbacks.Load();
            controller._menu.QuitRequested += () => callbacks.Quit();

            controller._interactor.MiningProgressChanged += controller._hud.SetMiningProgress;
            controller._interactor.NoticePosted += controller._hud.PostToast;
            controller._interactor.SelectionChanged += controller._hud.SetSelectedSlot;
            controller._inventory.CountChanged += controller._hud.SetCount;

            foreach (var kind in registry.PlaceableKinds)
            {
                controller._hud.SetCount(kind, inventory.GetCount(kind));
            }

            controller._hud.SetSelectedSlot(interactor.SelectedSlot);
            return controller;
        }

        /// <summary>Shows a HUD toast (also used by save/load feedback).</summary>
        public void Toast(string message)
        {
            _hud.PostToast(message);
        }

        public void SetMenuStatus(string message)
        {
            _menu.SetStatus(message);
        }
    }
}
