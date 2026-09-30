using UnityEngine;
using UnityEngine.Rendering;
using VoxelWorld.Core.Blocks;
using VoxelWorld.Core.Editing;
using VoxelWorld.Core.Generation;
using VoxelWorld.Core.Inventory;
using VoxelWorld.Core.Persistence;
using VoxelWorld.Core.World;
using VoxelWorld.Persistence;
using VoxelWorld.Player;
using VoxelWorld.UI;
using VoxelWorld.World;

namespace VoxelWorld.App
{
    /// <summary>
    /// Composition root. Builds every service explicitly (no service locator, no
    /// reflection), wires the event flow (input -> interactor -> editor -> grid ->
    /// renderer/UI) and owns the tiny Playing/Paused state machine plus save/load.
    /// The whole game lives behind this one component, so the scene stays trivial.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig config = new GameConfig();

        private enum GameState
        {
            Playing,
            Paused
        }

        private GameState _state = GameState.Playing;

        private BlockRegistry _registry;
        private WorldGrid _grid;
        private WorldEditor _editor;
        private WorldRenderer _renderer;
        private IWorldPersistence _persistence;
        private IPlayerInput _input;
        private PlayerMotor _motor;
        private PlayerInteractor _interactor;
        private Inventory _inventory;
        private GameUiController _ui;
        private PauseMenuView _menu;
        private Transform _worldRoot;

        public bool IsPlaying => _state == GameState.Playing;

        private void Awake()
        {
            Physics.autoSyncTransforms = true; // raycasts see same-frame cube edits
            SetupLighting();

            _worldRoot = new GameObject("World").transform;

            _registry = BlockRegistry.CreateDefault(config.CreateBlockTuning());
            var profile = config.CreateTerrainProfile();
            _grid = new WorldGrid(config.WorldChunksX, config.WorldChunksZ, config.ChunkSize, config.WorldHeight,
                seed => new HeightmapTerrainGenerator(seed, profile, config.WorldHeight), RandomSeed());
            _editor = new WorldEditor(_grid, _registry);
            _persistence = new JsonWorldPersistence(new FileSaveStorage(config.SaveFileName));

            var blockLayer = LayerMask.NameToLayer("Block");
            _renderer = new WorldRenderer(_grid, _registry, _worldRoot, config.ChunkSize,
                config.LoadRadius, config.UnloadRadius, config.ChunksBuiltPerFrame,
                config.PoolPrewarm, config.PoolMax, config.CastCubeShadows, blockLayer);
            _renderer.BuildWorldBoundaries(LayerMask.NameToLayer("WorldBoundary"));

            var spawnCenter = new Vector3Int(_grid.Size.x / 2, 0, _grid.Size.z / 2);
            _renderer.BuildImmediate(ChunkMath.ToChunkCoord(spawnCenter, config.ChunkSize), 1);

            BuildPlayer(spawnCenter, blockLayer);

            _input = CreateInput();
            _input.PlacePressed += _interactor.TryPlace;
            _input.SlotSelected += _interactor.SelectSlot;
            _input.PausePressed += TogglePause;
            _input.SaveRequested += SaveGame;
            _input.LoadRequested += LoadGame;

            BuildUi();

            ResumeGame(); // locks cursor, enables gameplay input, hides menu
        }

        private void Update()
        {
            _input.Pump();
            var dt = Time.deltaTime;

            if (IsPlaying)
            {
                var scroll = _input.ConsumeScrollSteps();
                if (scroll != 0)
                {
                    _interactor.Scroll(scroll);
                }

                _motor.Step(dt, _input.MoveAxis, _input.LookDelta, _input.IsSprintHeld, _input.IsJumpHeld);
                _interactor.Step(dt, _input.IsMineHeld);
            }

            _renderer.StreamAround(_motor.Position);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
            _renderer?.Dispose();
        }

        // ---- Game state ----

        public void TogglePause()
        {
            if (IsPlaying)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }

        private void PauseGame()
        {
            _state = GameState.Paused;
            _input.SetGameplayEnabled(false);
            _interactor.SetPaused(true);
            _menu.SetVisible(true);
            LockCursor(false);
        }

        private void ResumeGame()
        {
            _state = GameState.Playing;
            _input.SetGameplayEnabled(true);
            _interactor.SetPaused(false);
            _menu.SetVisible(false);
            LockCursor(true);
        }

        // ---- Save / Load ----

        public void SaveGame()
        {
            var data = new WorldSaveData
            {
                seed = _grid.Seed,
                player = new PlayerRecord
                {
                    px = _motor.Position.x,
                    py = _motor.Position.y,
                    pz = _motor.Position.z,
                    yaw = _motor.Yaw,
                    pitch = _motor.Pitch
                }
            };

            foreach (var pair in _grid.Deltas)
            {
                data.deltas.Add(new BlockDeltaRecord
                {
                    x = pair.Key.x,
                    y = pair.Key.y,
                    z = pair.Key.z,
                    kind = (int)pair.Value
                });
            }

            foreach (var pair in _inventory.Counts)
            {
                data.inventory.Add(new InventoryRecord { kind = (int)pair.Key, count = pair.Value });
            }

            _persistence.Save(data);
            _ui.Toast($"World saved ({_grid.Deltas.Count} changes).");
        }

        public void LoadGame()
        {
            if (!_persistence.TryLoad(out var data))
            {
                _ui.SetMenuStatus("No saved world found.");
                return;
            }

            // Same seed regenerates the same world; stored deltas replay the player's edits.
            _grid.Reset(data.seed);
            foreach (var delta in data.deltas)
            {
                _grid.ApplyDelta(new Vector3Int(delta.x, delta.y, delta.z), (BlockKind)delta.kind);
            }

            var counts = new System.Collections.Generic.Dictionary<BlockKind, int>();
            foreach (var record in data.inventory)
            {
                counts[(BlockKind)record.kind] = record.count;
            }

            _inventory.Restore(counts);

            _renderer.RebuildAll();
            var position = new Vector3(data.player.px, data.player.py, data.player.pz);
            _renderer.BuildImmediate(ChunkMath.ToChunkCoord(position, config.ChunkSize), 1);
            _motor.Teleport(position, data.player.yaw, data.player.pitch);

            _ui.SetMenuStatus($"World loaded ({data.deltas.Count} changes).");
            _ui.Toast("World loaded.");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Construction helpers ----

        private void BuildPlayer(Vector3Int spawnCenter, int blockLayer)
        {
            _inventory = new Inventory(_registry);

            var playerGo = new GameObject("Player");
            var playerLayer = Mathf.Max(0, LayerMask.NameToLayer("Player"));
            playerGo.layer = playerLayer;

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 60f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.06f;

            _motor = playerGo.AddComponent<PlayerMotor>();
            var head = new GameObject("Head").transform;
            head.SetParent(playerGo.transform, false);
            head.gameObject.layer = playerLayer;

            var camera = head.gameObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.Skybox;
            // Solid fallback shown if the procedural skybox shader is unavailable.
            camera.backgroundColor = config.SkyColor;
            camera.tag = "MainCamera";

            var size = _grid.Size;
            _motor.Init(controller, head, config.EyeHeight,
                config.WalkSpeed, config.SprintSpeed, config.JumpVelocity, config.Gravity,
                config.MouseSensitivity, 89f, Vector3.one, new Vector3(size.x - 1f, 0f, size.z - 1f));

            var ghostGo = new GameObject("PlacementGhost");
            var ghost = ghostGo.AddComponent<PlacementGhost>();
            ghost.Initialize(config.GhostValidColor, config.GhostInvalidColor);

            var surface = _grid.SurfaceHeightAt(spawnCenter.x, spawnCenter.z);
            _motor.Teleport(new Vector3(spawnCenter.x + 0.5f, surface + 1.1f, spawnCenter.z + 0.5f), 0f, 0f);

            _interactor = new PlayerInteractor(head, _grid, _editor, _inventory, _registry, ghost,
                config.Reach, blockLayer, playerLayer, config.BottomMessage, config.CeilingMessage);
        }

        private void BuildUi()
        {
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas();
            var hud = HudView.Build(canvas, _registry);
            _menu = PauseMenuView.Build(canvas);
            _ui = GameUiController.Create(canvas, hud, _menu, _inventory, _registry, _interactor, new MenuCallbacks
            {
                Resume = ResumeGame,
                Save = SaveGame,
                Load = LoadGame,
                Quit = QuitGame
            });
        }

        private IPlayerInput CreateInput()
        {
#if ENABLE_INPUT_SYSTEM
            return new InputSystemPlayerInput();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new LegacyPlayerInput();
#else
            Debug.LogError("[VoxelSandbox] No input backend is enabled. Enable the Input System package in Player Settings.");
            return new NullPlayerInput();
#endif
        }

        private int RandomSeed()
        {
            return config.UseFixedSeed ? config.FixedSeed : Random.Range(int.MinValue, int.MaxValue);
        }

        private void SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.shadows = config.CastCubeShadows ? LightShadows.Soft : LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = config.LoadRadius * config.ChunkSize * 0.5f;
            RenderSettings.fogEndDistance = Mathf.Max(10f, config.LoadRadius * config.ChunkSize - 2f);
            RenderSettings.fogColor = config.SkyColor;
            var skyboxShader = Shader.Find("Skybox/Procedural");
            if (skyboxShader != null)
            {
                RenderSettings.skybox = new Material(skyboxShader);
            }
            else
            {
                // Never let a stripped shader abort Awake: without a camera the build
                // boots to a bare gray window. The solid background color covers this.
                Debug.LogWarning("[VoxelSandbox] 'Skybox/Procedural' is unavailable in this build; " +
                                 "falling back to a solid sky color.");
            }
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>Last-resort input stub so the project compiles even with all input backends disabled.</summary>
        private sealed class NullPlayerInput : IPlayerInput
        {
            public Vector2 MoveAxis => Vector2.zero;
            public Vector2 LookDelta => Vector2.zero;
            public bool IsSprintHeld => false;
            public bool IsJumpHeld => false;
            public bool IsMineHeld => false;

            public event System.Action PlacePressed;
            public event System.Action<int> SlotSelected;
            public event System.Action PausePressed;
            public event System.Action SaveRequested;
            public event System.Action LoadRequested;

            public int ConsumeScrollSteps() => 0;
            public void Pump()
            {
            }

            public void SetGameplayEnabled(bool enabled)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
