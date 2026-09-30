# Voxel Sandbox

A compact Minecraft-style voxel sandbox built for **Unity 6.3 LTS**. It ships as a complete, ready-to-play project: procedurally generated terrain, mine-and-place gameplay with grid-aligned building, chunk streaming, an inventory hotbar, wireframe placement previews, save/load, and a full in-game UI — all implemented in plain C# with zero external assets.

## Requirements

- **Unity 6.3 LTS** (the Input System, UI and Test Framework packages are declared in `Packages/manifest.json` and install automatically on first open)
- No additional assets, plugins, or manual project configuration required

## Controls

| Input | Action |
| --- | --- |
| `W` `A` `S` `D` | Move |
| Mouse | Look |
| `Space` | Jump |
| `Left Shift` | Sprint |
| Left mouse (hold) | Mine the targeted cube |
| Right mouse | Place the selected cube |
| `1` – `4` / Mouse wheel | Select hotbar slot |
| `Esc` | Pause menu (with controls reference) |
| `F5` / `F9` | Save / Load world |

## Blocks & Mining

Terrain is color-banded by depth, and each block type takes a different time to break (defaults below; all tunable in `GameConfig`):

| Block | Look | Default mining time | Notes |
| --- | --- | --- | --- |
| Stone | Gray | 1.5 s | Deep underground layer |
| Grass | Green | 0.75 s | Normal surface layer |
| Sand | Tan | 0.5 s | Found in low-lying areas |
| Snow | White | 0.3 s | Caps peaks above the snow line |
| Bedrock | Dark gray | Unbreakable | World floor |

Gameplay rules:

- **Mining** requires holding the left mouse button; a radial progress bar on the HUD shows the break progress. The interact reach is 6 m by default.
- **Placing** snaps to the voxel grid. A wireframe preview shows where the cube will land — **green** means the spot is valid, **red** means it is blocked (for example, intersecting the player or another cube).
- The world has hard limits in both directions: digging stops at the **bedrock floor** and building stops at the **world ceiling**. Each prints a hint message when hit.

## World Generation

The terrain is a Perlin-noise heightmap (multi-octave, deterministic from the world seed) over a world of **12 × 12 chunks of 16 × 16 blocks, 48 blocks tall** by default. Surface height, noise frequency/octaves, the snow line, soil depth, and the sand elevation band are all exposed in `GameConfig`, so new landscapes are a few sliders away.

The world streams around the player: chunks inside the load radius are built (a few per frame at most, to avoid hitches) and chunks beyond the unload radius are released back to a pool. Only blocks with at least one air-facing side are instantiated; fully buried voxels remain pure data. Solid boundary walls close off the world edges.

## Saving & Loading

`F5` writes a JSON save; `F9` loads the latest one. Saves are compact by design — they store the **world seed plus a sparse list of player edits** (deltas), along with the player position, camera rotation, and inventory. Loading regenerates the base terrain from the seed and replays the deltas, so even large worlds save to a small file.

The save file lives at `<Application.persistentDataPath>/voxelsandbox_save.json` (the file name is configurable).

## Configuration

Every tunable lives in one place: select the `Bootstrap` GameObject and edit the **Game Config** component in the Inspector. Highlights:

- **World** — fixed or random seed, world dimensions, chunk size, world height
- **Streaming & Pooling** — load/unload radii, chunk builds per frame, pool sizes, shadow toggles
- **Terrain** — surface height range, noise frequency and octaves, snow line, soil depth, sand band
- **Mining** — break time per block type
- **Player** — walk/sprint speed, jump, gravity, mouse sensitivity, reach, eye height
- **Visuals** — sky color, placement ghost valid/invalid colors
- **Save / Messages** — save file name and the bottom/ceiling hint texts


