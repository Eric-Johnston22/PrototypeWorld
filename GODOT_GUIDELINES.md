# Godot 4 C# Development Guidelines

> Reference guide for puzzle-prototype-1. Based on Godot 4.6.1 with C#/.NET 8.

---

## Table of Contents

1. [Language Choice: C#](#language-choice-c)
2. [Project Folder Structure](#project-folder-structure)
3. [Scene Organization](#scene-organization)
4. [Script Architecture](#script-architecture)
5. [Signals & Event Bus](#signals--event-bus)
6. [Autoloads / Singletons](#autoloads--singletons)
7. [State Management](#state-management)
8. [Resources (.tres / .res)](#resources-tres--res)
9. [C# Performance Notes](#c-performance-notes)
10. [General Best Practices](#general-best-practices)

---

## Language Choice: C#

We are using **C#** throughout this project.

### Pros
- Full OOP: interfaces, generics, access modifiers, extension methods, namespaces
- Significantly faster runtime than GDScript for CPU-intensive logic
- Access to the entire .NET ecosystem (NuGet packages)
- Familiar tooling (Visual Studio / Rider)
- Transferable skills outside of Godot

### Cons
- **No web export** (still under development by Godot team)
- **No GDExtension bindings** — cannot call GDExtensions directly from C# (workaround: bridge through GDScript)
- Fewer Godot-specific tutorials than GDScript
- Hot-reload is more limited than GDScript

### Setup Notes
- Use **Visual Studio** or **JetBrains Rider** for best C# + Godot experience
- Godot 4.2+ uses **.NET 8** — ensure your SDK matches
- All node scripts require the `partial` keyword (Godot source generators depend on it)
- Lifecycle methods are **PascalCase**: `_Ready()`, `_Process()`, `_PhysicsProcess()`

---

## Project Folder Structure

```
puzzle-prototype-1/
├── addons/                    # Third-party plugins and tools
├── assets/
│   ├── audio/
│   ├── sprites/
│   ├── fonts/
│   └── materials/
├── scenes/                    # .tscn files, organized by feature
│   ├── levels/
│   ├── ui/
│   └── components/
├── src/                       # All C# source files
│   ├── autoloads/             # Global singletons (EventBus, GameManager, etc.)
│   ├── components/            # Reusable node behaviors
│   ├── entities/              # Game objects (player, puzzle pieces, etc.)
│   ├── managers/              # System managers (audio, save, input)
│   ├── systems/               # State machines, game systems
│   ├── ui/                    # UI controller scripts
│   └── utilities/             # Helpers and extension methods
├── resources/
│   ├── data/                  # Game data (level configs, puzzle definitions)
│   └── settings/              # Audio, graphics, game settings
├── tests/
└── GODOT_GUIDELINES.md
```

### Naming Conventions

| Item | Convention | Example |
|---|---|---|
| Folders | `snake_case` | `puzzle_pieces/` |
| Scene files | `snake_case` | `main_menu.tscn` |
| C# files | `PascalCase` | `PuzzleManager.cs` |
| Node names | `PascalCase` | `PlayerCamera` |
| Signals | `PascalCase` + `EventHandler` suffix | `PuzzleSolvedEventHandler` |
| Resources | `snake_case` | `level_01_config.tres` |

### Important
- **Always move/rename files inside the Godot editor**, not in the file system directly. This keeps `.import` metadata and internal references intact.

---

## Scene Organization

### Core Rules

- **One script per scene root** — the root node's script is the controller for that scene
- **Subscenes must be self-contained** — a subscene should function without knowledge of its parent. No hardcoded paths to sibling or parent nodes
- **Think relationally, not spatially** — a node should be a child of whatever it depends on, not just what is nearby in the tree
- **Limit scene inheritance to one level** — if you need more, you likely want composition instead

### When to Create a Subscene

- The content appears in more than one place
- The node group becomes complex enough to manage on its own
- You want to isolate a reusable UI element or game component

### Scene Tree Structure Example

```
Main (Node)
├── GameWorld (Node2D)
│   ├── PuzzleBoard (puzzle_board.tscn)   ← self-contained subscene
│   └── Player (player.tscn)             ← self-contained subscene
└── UI (CanvasLayer)
    ├── HUD (hud.tscn)
    └── PauseMenu (pause_menu.tscn)
```

---

## Script Architecture

### Composition Over Inheritance

Favor attaching multiple small component nodes to an entity over building deep inheritance chains. Components are self-contained and reusable.

```csharp
// A component doesn't need to know what owns it
public partial class HealthComponent : Node
{
    [Export] public int MaxHealth { get; set; } = 100;
    private int _currentHealth;

    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void DiedEventHandler();

    public override void _Ready() => _currentHealth = MaxHealth;

    public void TakeDamage(int amount)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
        if (_currentHealth == 0)
            EmitSignal(SignalName.Died);
    }
}
```

### Dependency Injection via [Export]

Prefer injecting dependencies through `[Export]` over reaching for autoloads directly. This makes code testable and decoupled.

```csharp
// Prefer this:
public partial class PuzzlePiece : Node
{
    [Export] public PuzzleManager PuzzleManager { get; set; }
}

// Over this:
public partial class PuzzlePiece : Node
{
    private PuzzleManager _manager => GetNode<PuzzleManager>("/root/PuzzleManager");
}
```

### MVC with Resources

- **Model** — Custom `Resource` class holding data with signals for change notification
- **View** — Scene nodes / UI handling display
- **Controller** — Node script handling input and updating the model

```csharp
[GlobalClass]
public partial class LevelData : Resource
{
    [Export] public string LevelName { get; set; }
    [Export] public int MoveLimit { get; set; }
    [Export] public Godot.Collections.Array<Vector2I> PuzzleLayout { get; set; }
}
```

---

## Signals & Event Bus

### Two-Tier Signal System

| Tier | Use For | How |
|---|---|---|
| **Direct signals** | Local, parent-child communication within a scene | Connect in `_Ready()` |
| **Event Bus** | Cross-system communication (UI ↔ gameplay, manager ↔ entity) | Global autoload |

### Event Bus Setup

```csharp
// src/autoloads/EventBus.cs
public partial class EventBus : Node
{
    // Puzzle events
    [Signal] public delegate void PuzzleSolvedEventHandler();
    [Signal] public delegate void MoveMadeEventHandler(int movesRemaining);
    [Signal] public delegate void PuzzleResetEventHandler();

    // Game events
    [Signal] public delegate void LevelLoadedEventHandler(int levelIndex);
    [Signal] public delegate void LevelCompleteEventHandler(int levelIndex, float timeElapsed);
    [Signal] public delegate void GamePausedEventHandler(bool isPaused);

    // UI events
    [Signal] public delegate void ScoreUpdatedEventHandler(int newScore);
}
```

Register in `Project > Project Settings > Autoload` as `EventBus`.

### Usage

```csharp
// Emitting
EventBus.EmitSignal(EventBus.SignalName.PuzzleSolved);

// Listening
public override void _Ready()
{
    EventBus.PuzzleSolved += OnPuzzleSolved;
}

public override void _ExitTree()
{
    // Always disconnect to avoid memory leaks
    EventBus.PuzzleSolved -= OnPuzzleSolved;
}

private void OnPuzzleSolved() { /* ... */ }
```

### Rules
- Use **direct signals** for communication within the same scene
- Use **EventBus** when two nodes have no common parent or are in different scenes
- Always **disconnect signals** in `_ExitTree()` when connecting to the EventBus from a node that can be freed

---

## Autoloads / Singletons

### When to Use

- EventBus (signals only — no state)
- AudioManager
- SaveManager
- GameManager (high-level game flow only)

### When NOT to Use

- Per-level data → use Resources instead
- Anything you want to unit test → inject via `[Export]` instead
- Data that should reset between scenes

### Keep Them Focused

```csharp
// Bad: one massive singleton
public partial class GameManager : Node { /* 600 lines of everything */ }

// Good: single-purpose singletons
public partial class EventBus : Node    { /* signals only */ }
public partial class AudioManager : Node { /* audio only */ }
public partial class SaveManager : Node  { /* save/load only */ }
public partial class GameManager : Node  { /* flow control only */ }
```

---

## State Management

### Game-Level State (enum + GameManager)

For top-level game flow:

```csharp
public enum GameState { MainMenu, Playing, Paused, LevelComplete, GameOver }

public partial class GameManager : Node
{
    private GameState _state = GameState.MainMenu;

    [Signal] public delegate void StateChangedEventHandler(GameState newState);

    public void ChangeState(GameState newState)
    {
        ExitState(_state);
        _state = newState;
        EnterState(_state);
        EmitSignal(SignalName.StateChanged, _state);
    }

    private void EnterState(GameState state) => state switch
    {
        GameState.MainMenu     => LoadMainMenu(),
        GameState.Playing      => StartLevel(),
        GameState.Paused       => PauseGame(),
        GameState.LevelComplete => ShowLevelComplete(),
        GameState.GameOver     => ShowGameOver(),
        _                      => throw new ArgumentOutOfRangeException()
    };

    private void ExitState(GameState state) { /* cleanup per state */ }
}
```

### Entity-Level State (Node-Based State Machine)

For complex entity behavior (player, puzzle pieces with multi-step interactions):

```
PlayerStateMachine (Node)
├── IdleState
├── DraggingState
├── SnapState
└── LockedState
```

```csharp
// Base state
public partial class State : Node
{
    protected Node Owner => GetParent().GetParent();

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update(double delta) { }
}

// State machine
public partial class StateMachine : Node
{
    [Export] public State InitialState { get; set; }
    private State _current;

    public override void _Ready()
    {
        _current = InitialState;
        _current.Enter();
    }

    public void TransitionTo(State next)
    {
        _current.Exit();
        _current = next;
        _current.Enter();
    }

    public override void _Process(double delta) => _current.Update(delta);
}
```

---

## Resources (.tres / .res)

### File Format

| Format | Use When |
|---|---|
| `.tres` (text) | Version-controlled data, level configs, anything edited in Inspector |
| `.res` (binary) | Large assets, save files, performance-sensitive data |

Prefer `.tres` for everything you'll edit or commit to Git — it diffs cleanly.

### Custom Resource Classes

```csharp
// src/entities/LevelConfig.cs
[GlobalClass]
public partial class LevelConfig : Resource
{
    [Export] public string LevelName { get; set; } = "Level 1";
    [Export] public int MoveLimit { get; set; } = 30;
    [Export] public int GridWidth { get; set; } = 5;
    [Export] public int GridHeight { get; set; } = 5;
    [Export] public Texture2D Background { get; set; }
}
```

Once registered with `[GlobalClass]`, these appear in the Godot Inspector and can be created/edited directly in the editor.

### Loading Resources

```csharp
// Load at startup (synchronous)
var config = GD.Load<LevelConfig>("res://resources/data/level_01.tres");

// Load at runtime (async, for large resources)
ResourceLoader.LoadThreadedRequest("res://resources/data/level_01.tres");
```

### User Data (Save Files)

Always save to `user://`, never `res://`:

```csharp
// Save
ResourceSaver.Save(saveData, "user://save.tres");

// Load
var saveData = GD.Load<SaveData>("user://save.tres");
```

---

## C# Performance Notes

Every C# ↔ Godot interaction crosses a native interop boundary (Variant marshalling). Generally not a concern, but be mindful in hot paths.

### Cache Node References

```csharp
// Bad: GetNode on every frame
public override void _Process(double delta)
{
    GetNode<Sprite2D>("Sprite").Position = newPos; // interop call every frame
}

// Good: cache in _Ready
private Sprite2D _sprite;

public override void _Ready()
{
    _sprite = GetNode<Sprite2D>("Sprite");
}

public override void _Process(double delta)
{
    _sprite.Position = newPos; // cheap
}
```

### Prefer [Export] for Node References

```csharp
// Fragile: path string can break if tree changes
var label = GetNode<Label>("UI/HUD/ScoreLabel");

// Robust: wired in editor, refactor-safe
[Export] public Label ScoreLabel { get; set; }
```

### Avoid Allocations in _Process

- Don't create `new` objects every frame
- Reuse collections where possible
- Be mindful of LINQ in hot paths (it allocates enumerators)

### IsInstanceValid for Freed Nodes

Godot nodes can be in a "freed but not null in C#" state. Use `IsInstanceValid()` instead of a null check:

```csharp
// Bad
if (someNode != null) someNode.DoThing();

// Good
if (IsInstanceValid(someNode)) someNode.DoThing();
```

---

## General Best Practices

### Physics
- Godot 4.6 uses **Jolt Physics** by default for 3D (production-ready, previously experimental)
- **Direct3D 12** is the default renderer on Windows as of 4.6 — better stability

### Version Control
- Use **Git**
- Add `.godot/` to `.gitignore` (auto-generated cache, don't commit)
- `.tscn` and `.tres` files are text-based and diff cleanly
- Commit `project.godot` and all `.import` files

### Editor
- Docks/panels are now movable and floatable in Godot 4.6 — customize your layout
- Use `call_deferred()` when adding/removing nodes mid-frame to avoid crashes
- Profile before optimizing — use Godot's built-in profiler, don't guess

### Upgrade Safety
- Always back up / commit before upgrading Godot versions
- Check the [Godot Migration Guide](https://docs.godotengine.org/en/stable/tutorials/migrating/index.html) for breaking changes
- Test thoroughly after any upgrade, even minor 4.x releases

---

## Quick Reference: Decision Guide

| Question | Answer |
|---|---|
| Where does shared data live? | Custom `Resource` + autoload or `[Export]` injection |
| How do distant nodes communicate? | EventBus signals |
| How do local nodes communicate? | Direct signals |
| When do I use an Autoload? | Truly global, session-persistent concerns only |
| When do I split a scene? | When content is reused or becomes hard to manage |
| How deep should inheritance go? | One level max — then switch to composition |
| Where do I save user data? | `user://` path only |
| `.tres` or `.res`? | `.tres` for anything in Git; `.res` for large/perf-sensitive data |

---

## Sources

- [Godot Best Practices Documentation](https://docs.godotengine.org/en/stable/tutorials/best_practices/index.html)
- [Godot Scene Organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
- [Godot Autoloads vs Internal Nodes](https://docs.godotengine.org/en/stable/tutorials/best_practices/autoloads_versus_internal_nodes.html)
- [Godot C# Basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)
- [GDScript vs C# — Chickensoft](https://chickensoft.games/blog/gdscript-vs-csharp)
- [Design Patterns in Godot — GDQuest](https://www.gdquest.com/tutorial/godot/design-patterns/intro-to-design-patterns/)
- [Event Bus Pattern — GDQuest](https://www.gdquest.com/tutorial/godot/design-patterns/event-bus-singleton/)
- [Finite State Machine — GDQuest](https://www.gdquest.com/tutorial/godot/design-patterns/finite-state-machine/)
- [Godot 4.6 Release — Game Developer](https://www.gamedeveloper.com/programming/godot-4-6-is-here-with-a-fresh-look-and-a-promise-to-prioritize-workflow)
