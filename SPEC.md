# CubeGame — Technical Specification

## 1. Overview

CubeGame is a C#/.NET 9 desktop application that simulates a 3×3 Rubik's cube and presents a "match the face" game. The primary implementation uses Avalonia for cross-platform support; a legacy Windows Forms prototype is also included but not wired into the solution.

## 2. Architecture

### 2.1 Solution Layout

```
CubeGame.sln
├── CubeGame.Avalonia/          # Active, cross-platform (Avalonia UI)
└── CubeGame.WinForms/          # Legacy, Windows-only, not in solution
```

### 2.2 Dependency Graph

```
MainWindow
  ├── Cube3x3          (Scene)     — 27-cubie grid model
  ├── CubeRenderer     (Render)    — 3D projection, view rotation, face sorting
  ├── GameState        (Game)      — Score, combo, accuracy, target
  ├── Input            (Game)      — Key state tracking
  ├── Overlay          (UI)        — HUD drawing & hit testing
  ├── SolveController  (AI)        — Move recording, undo queue, solve lifecycle
  ├── AiButtonOverlay  (AI)        — AI button & status drawing
  └── LayerTurnAnimation (Render)  — Single-layer turn animation
```

### 2.3 Namespaces

| Namespace | Responsibility |
|---|---|
| `CubeGame.Avalonia` | Window lifecycle, keyboard dispatch, game loop (16 ms tick) |
| `CubeGame.Avalonia.Scene` | `Cube3x3` (3×3×3 cubie array), `Cubie` (position + 6 sticker colors) |
| `CubeGame.Avalonia.Render` | `Camera` (dot-product perspective), `CubeRenderer` (view basis, projection, painter's sort), `LayerTurnAnimation` (smoothstep rotation), `ViewState` (serialization DTO) |
| `CubeGame.Avalonia.UI` | `Overlay` (score, target, buttons, help text) |
| `CubeGame.Avalonia.Game` | `GameState` (scoring, combo, accuracy), `Input` (boolean key states), `CubeMode` (enum), `CubeStateStore` (JSON persistence) |
| `CubeGame.Avalonia.AI` | `SolveController` (undo stack, solution queue), `AiSolverService` (OpenRouter API or local fallback), `AiButtonOverlay` |
| `CubeGame.Avalonia.Math` | `Vector3` (custom 3D vector struct) |

## 3. Cube Model

### 3.1 Cubie

Each cubie stores:
- **Grid position** `(GX, GY, GZ)` in range `{-1, 0, 1}`
- **Sticker colors** `Color[6]` indexed by `FaceDir` enum: `Right, Left, Up, Down, Front, Back`

Initial colors:
| Face  | Color |
|-------|-------|
| Right | Red   |
| Left  | Orange|
| Up    | White |
| Down  | Yellow|
| Front | Green |
| Back  | Blue  |

Interior (non-surface) faces are transparent.

### 3.2 Layer Rotation

`Cube3x3.RotateLayer(LayerAxis axis, int layer, bool clockwise)`:
1. Selects all cubies on the given layer index `(-1, 0, +1)` along the given axis.
2. Removes them from the grid.
3. Rotates each cubie's position by a 90° rotation matrix around the axis.
4. Rotates each cubie's stickers via `Cubie.RotateStickers()`.
5. Places cubies back into the grid at the new positions.

## 4. Rendering Pipeline

### 4.1 View Basis

`CubeRenderer` maintains three orthonormal basis vectors `(_viewX, _viewY, _viewZ)`.
Initial orientation: `RotateX(-0.35)` then `RotateY(0.55)`, producing an isometric-like view.

### 4.2 Projection

Per frame for all 27 cubies:
1. Compute 8 corner vertices of each cubie in world space `([-0.5..0.5]³ relative to cubie center)`.
2. Transform by view basis: `vertex · _viewX/Y/Z`.
3. If a `LayerTurnAnimation` is active, cubies on the turning layer have vertices additionally rotated by the animation's current angle.
4. Project through `Camera` via dot-product perspective.
5. For each of the 6 faces (2 triangles), test normal against camera direction — cull back faces.
6. Collect all remaining faces, sort by average depth (painter's algorithm back-to-front).
7. Draw each face as a rounded polygon.

### 4.3 Camera

`Camera` is positioned at `(0, 0, -6)` looking at `(0, 0, 0)` with a simple dot-product projection:
```
projected = screenCenter + (vertex.X / vertex.Z * focalLength, vertex.Y / vertex.Z * focalLength)
```
where `focalLength = screenHeight / tan(FOV/2)`.

### 4.4 Animation

`LayerTurnAnimation` interpolates progress from 0→1 at `0.042` per tick (≈24 ticks ≈ 384 ms at 60 fps) using an ease-in-out smoothstep curve:
```
EaseInOut(t) = t * t * (3 - 2 * t)
```
The `Angle` property returns `EaseInOut(progress) * π/2`.

## 5. Game Loop

### 5.1 Tick (16 ms interval)

```
1. Process keyboard input → camera rotation / layer turns
2. Apply idle auto-rotation if enabled and no arrow keys pressed
3. Advance active layer turn animation
4. If animation completes → commit RotateLayer to model
5. Dequeue next AI solver move if pending
6. InvalidateVisual() → triggers Render
```

### 5.2 Scoring

- **Match**: +100 base points
- **Combo bonus**: combo ≥ 3 → +20, combo ≥ 5 → +50
- **Miss**: combo resets to 0
- **Accuracy**: `correct / total × 100%`
- Target face is randomly chosen from `{Front, Right, Back, Left, Top, Bottom}` after each match.

### 5.3 Front Face Detection

`CubeRenderer.GetFrontFaceName()` determines the front-most cubie by scanning all 27 cubies and picking the one with the highest projected Z value (closest to camera). The face of that cubie whose normal is most aligned with the view direction is returned as the current front face.

## 6. AI Solver

### 6.1 Architecture

`SolveController` implements an undo-based solver:

1. **Recording**: Every manual `RotateLayer` call with `recordMove=true` pushes `(axis, layer, clockwise)` onto `_moveHistory` (a stack).
2. **Solving**: `RequestSolve()` inverts the stack into a queue (reverse direction, reverse chronological order) and clears the history.
3. **Execution**: On each tick, if a queued move exists and no animation is running, dequeue and execute `RotateLayer(recordMove: false)`.
4. **Completion**: When the queue empties, `NotifyComplete()` signals done.

### 6.2 AI Commentary

`AiSolverService` optionally calls OpenRouter API if `OPENROUTER_API_KEY` or `OPENAI_API_KEY` is set. Fallback models: Gemma 3 27B, Llama 3.2 11B, Mistral 7B (free tiers). Without an API key, hardcoded Korean messages are used.

## 7. Persistence

`CubeStateStore` saves/loads cube state via `System.Text.Json` to `~/.local/share/CubeGame/cube-state.json`:
- All 27 cubie positions and sticker colors
- View basis angles (9 floats in `ViewState`)
- Saved on every completed layer turn and on app close; loaded at startup.

## 8. Build & Run Requirements

- .NET SDK 9.0
- Avalonia 12.0.3, Fluent theme, Inter fonts (restored via NuGet)

```bash
dotnet build CubeGame.sln
```

### macOS
```bash
./run-avalonia.sh
```

### Windows / Linux
```bash
dotnet run --project CubeGame.Avalonia
```

## 9. Controls Reference

| Input | Action |
|---|---|
| ← → ↑ ↓ | Rotate camera |
| U / I / O | Top / Middle / Bottom layer (Y-axis, resolved to screen-aligned axis) |
| J / K / L | Left / Center / Right layer (X-axis, resolved to screen-aligned axis) |
| Shift + layer key | Reverse rotation direction |
| T | Toggle auto-rotation |
| Space | Check front face match |
| 1 / NumPad1 | Standard cube display mode |
| 2 / NumPad2 | Current cube display mode (shows cubie coordinate) |
| R | Reset score / combo / history |
| H | Toggle help overlay |
| Escape | Exit |
| Mouse click | All HUD buttons clickable |
