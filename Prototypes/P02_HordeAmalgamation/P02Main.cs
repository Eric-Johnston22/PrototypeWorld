using Godot;

namespace Hoarders;

/// <summary>
/// P02 scene root. Wires signals between Vacuum, HUD, and HordeAmalgamation.
/// </summary>
public partial class P02Main : Node3D
{
    public override void _Ready()
    {
        var vacuum = GetNode<Vacuum>("Player/Head/Vacuum");
        var hud = GetNode<HUD>("HUD");
        var amalgamation = GetNode<HordeAmalgamation>("HordeAmalgamation");

        vacuum.ItemCollected += hud.OnItemCollected;
        vacuum.VacuumStateChanged += hud.OnVacuumStateChanged;

        amalgamation.Died += OnAmalgamationDied;
    }

    private void OnAmalgamationDied()
    {
        GD.Print("The Horde Amalgamation is defeated!");
        // TODO: trigger win state / room clear fanfare
    }
}
