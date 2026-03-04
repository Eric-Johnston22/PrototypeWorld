# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## Project Overview

**Hoarders** is a first-person vacuum game where the player uses the "Suck-O-Matic 3000" to battle living, writhing rooms full of filth. Inspired by Luigi's Mansion and TF2's whimsical style.

This repo is a **monorepo of prototypes** — each prototype lives in its own folder under `Prototypes/` and answers a specific design question. Shared, battle-tested code lives in `Shared/`. The `VerticalSlice/` folder is reserved for when a concept is fully validated.

## Engine & Runtime

- **Engine:** Godot 4.6 with C#/.NET support
- **Physics:** Jolt Physics (3D)
- **Rendering:** Forward Plus (D3D12 on Windows)
- **.NET:** SDK 8.0, Godot.NET.Sdk 4.6.0

## Repo Structure

```
hoarders/
├── CLAUDE.md
├── GODOT_GUIDELINES.md       ← C# + Godot patterns reference
├── project.godot             ← Shared Godot project settings
├── Shared/                   ← Reusable, stable code & assets
│   ├── Scripts/              ← Core scripts used across prototypes
│   ├── Scenes/               ← Reusable subscenes (player, vacuum, HUD)
│   └── Assets/
│       ├── Audio/
│       ├── Materials/
│       └── Models/
├── Prototypes/
│   ├── P01_VacuumCore/       ← Basic suction/blow feel prototype
│   └── P02_HoardAmalgamation/← Room-as-mob combat prototype
└── VerticalSlice/            ← Future: validated full-game slice
```

## Build & Lint Commands

```bash
# Build the C# project
dotnet build

# Restore NuGet dependencies
dotnet restore

# Lint/format C# files
dotnet format
```

## Prototype Rules

1. **Prototype code is not production code.** Write it dirty — answer the design question, then move on.
2. **Never refactor prototype code** — if something is worth keeping, migrate it clean into `Shared/`.
3. **One question per prototype.** Each `P##_Name` folder should have a one-line README saying what question it answers.
4. **Shared/ stays clean.** Only code that's been validated across at least one prototype moves here.

## Architecture

### Autoloads

| Name | Script | Purpose |
|---|---|---|
| `GameManager` | `Shared/Scripts/GameManager.cs` | Game flow (MainMenu → Playing → Paused), owns Escape key |
| `SettingsManager` | `Shared/Scripts/SettingsManager.cs` | Loads/saves/applies user settings to `user://settings.cfg` |

### Shared Systems

- **`Shared/Scripts/PlayerController.cs`** — FPS movement, mouse look, jump, sprint. Supports `InvertY` and reads settings from `SettingsManager` on startup.
- **`Shared/Scripts/Vacuum.cs`** — Suction/blow mechanics, signals, attachment system
- **`Shared/Scripts/VacuumableObject.cs`** — RigidBody3D component for suckable objects
- **`Shared/Scripts/HUD.cs`** — In-game HUD: crosshair, capacity bar, item notifications
- **`Shared/Scripts/MainMenu.cs`** — Main menu screen (Play, Settings, Quit). Scene: `Shared/Scenes/MainMenu.tscn`
- **`Shared/Scripts/PauseMenu.cs`** — Pause overlay (Resume, Settings, Quit to Menu). Scene: `Shared/Scenes/PauseMenu.tscn`. Spawned by prototype Main scripts.
- **`Shared/Scripts/SettingsMenu.cs`** — Settings panel embedded in MainMenu and PauseMenu (mouse sensitivity, FOV, volume, invert Y, screenshake)

### Game Flow

```
MainMenu.tscn (main scene)
  → PLAY → GameManager.StartGame() → loads prototype scene
  → Escape → GameManager.PauseGame() → shows PauseMenu
  → Resume → GameManager.ResumeGame() → hides PauseMenu
  → Quit to Menu → GameManager.QuitToMainMenu() → loads MainMenu.tscn
```

Each prototype's Main script must spawn a PauseMenu instance in `_Ready()`:
```csharp
var pauseScene = GD.Load<PackedScene>("res://Shared/Scenes/PauseMenu.tscn");
AddChild(pauseScene.Instantiate());
```

### Prototype: P01_VacuumCore

**Question:** Does the vacuum feel fun to use against normal scattered objects?

Reference implementation: `C:\Users\ericm\Game Dev\vacuum-prototype`

### Prototype: P02_HoardAmalgamation

**Question:** Can a room-as-mob encounter with layered junk and vacuum attachments be fun?

Key design goals:
- Room is a living enemy with phases (Idle → Aggressive → Desperate)
- Player peels back layers of junk to expose weak points / the core
- Certain junk types require specific nozzle attachments to vacuum
- Enemy throws objects at player (ties into existing blow mechanic)

## Conventions

- C# namespace: `Hoarders`
- All files: UTF-8, LF line endings (enforced via `.gitattributes` + `.editorconfig`)
- Never commit `.godot/` — it's git-ignored and auto-generated
- Objects that can be vacuumed must be `RigidBody3D` in the `"vacuumable"` group
- See `GODOT_GUIDELINES.md` for full C# patterns, signals, state machines, and resources
