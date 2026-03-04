using Godot;

namespace Hoarders;

/// <summary>
/// P02 scene root. Wires signals between Vacuum, HUD, and HoardAmalgamation.
/// </summary>
public partial class P02Main : Node3D
{
    public override void _Ready()
    {
        var vacuum = GetNode<Vacuum>("Player/Head/Vacuum");
        var hud = GetNode<HUD>("HUD");
        var amalgamation = GetNode<HoardAmalgamation>("HoardAmalgamation");
        var player = GetNode<PlayerController>("Player");

        vacuum.ItemCollected += hud.OnItemCollected;
        vacuum.VacuumStateChanged += hud.OnVacuumStateChanged;

        player.HealthChanged += hud.OnHealthChanged;
        player.PlayerDied += OnPlayerDied;

        amalgamation.Died += OnAmalgamationDied;

        // Spawn pause menu (hidden by default, shown by GameManager on Escape)
        var pauseScene = GD.Load<PackedScene>("res://Shared/Scenes/PauseMenu.tscn");
        AddChild(pauseScene.Instantiate());
    }

    private void OnAmalgamationDied()
    {
        GD.Print("The Hoard Amalgamation is defeated!");
        // TODO: trigger win state / room clear fanfare
    }

    private void OnPlayerDied()
    {
        GD.Print("Player died!");
        // Return to main menu after a short delay
        var timer = GetTree().CreateTimer(2.0);
        timer.Timeout += () => GameManager.Instance?.QuitToMainMenu();
    }
}
