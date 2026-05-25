# Repository Guidelines

## Project Overview

CubeGame is a C#/.NET 9 desktop cube game prototype. The active app is the Avalonia project in `CubeGame.Avalonia`; `CubeGame.sln` currently includes only that project. A WinForms prototype also exists in `CubeGame.WinForms`, but it is not wired into the solution.

The Avalonia app renders a 3x3 cube manually with `DrawingContext`, handles keyboard input, and overlays score/target/help text. The intended game loop is: rotate the cube with arrow keys, press Space to match the currently visible face against the target, reset with `R`, toggle help with `H`, and exit with Escape.

## Important Paths

- `CubeGame.sln`: solution file for the Avalonia app.
- `CubeGame.Avalonia/`: primary cross-platform desktop app.
- `CubeGame.Avalonia/MainWindow.cs`: window setup, timer loop, keyboard dispatch, render surface.
- `CubeGame.Avalonia/Game/`: input and score state.
- `CubeGame.Avalonia/Scene/`: cubie and 3x3 cube model.
- `CubeGame.Avalonia/Render/`: camera/projection and cube renderer.
- `CubeGame.Avalonia/UI/`: overlay drawing.
- `CubeGame.WinForms/`: older Windows-only prototype/reference implementation.
- `run-avalonia.sh`: macOS helper that builds the Avalonia project and launches it as a temporary `.app` under `.run/`.

## Build And Run

Use .NET SDK 9.

```bash
dotnet build CubeGame.sln /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
```

On macOS, use:

```bash
./run-avalonia.sh
```

VS Code is configured with `.vscode/tasks.json` and `.vscode/launch.json` for the Avalonia project.

## Current Known State

- There is no git repository initialized at this path.
- There is no README yet.
- `dotnet build CubeGame.sln` currently succeeds.
- The Avalonia app has two display modes: `Standard cube` focuses on normal 3x3 Rubik's cube face names, and `Current cube` also shows the currently frontmost cubie coordinate for debugging.
- The cube uses the common color pairing: white/yellow, red/orange, and green/blue. The initial visible front face is green.
- The app models a 3x3x3 cubie cube. Top/Middle/Bottom and Left/Center/Right layer turns update cubie positions and sticker orientation.
- Layer turns animate visually first, then commit the cube state at the end of the 90-degree turn.

## Coding Notes

- Keep the Avalonia project as the primary implementation unless explicitly asked to work on WinForms.
- Prefer small, focused fixes in the existing manual-rendering architecture before introducing a new rendering stack.
- Avoid committing generated output under `bin/`, `obj/`, or `.run/`.
- When changing rendering behavior, verify with `dotnet build CubeGame.sln` first, then run the app with `./run-avalonia.sh` if a visual check is needed.
