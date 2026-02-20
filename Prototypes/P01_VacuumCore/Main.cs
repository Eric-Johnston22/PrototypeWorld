using Godot;

namespace Hoarders;

/// <summary>
/// P01 scene root. Wires the Vacuum signals to the HUD.
/// </summary>
public partial class Main : Node3D
{
	public override void _Ready()
	{
		var vacuum = GetNode<Vacuum>("Player/Head/Vacuum");
		var hud = GetNode<HUD>("HUD");

		vacuum.ItemCollected += hud.OnItemCollected;
		vacuum.VacuumStateChanged += hud.OnVacuumStateChanged;
	}
}
