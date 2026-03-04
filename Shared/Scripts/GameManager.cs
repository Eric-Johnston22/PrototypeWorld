using Godot;

namespace Hoarders;

/// <summary>
/// Autoload that manages high-level game flow: MainMenu → Playing → Paused.
/// Owns the Escape key — pauses/resumes the game and shows/hides the PauseMenu.
/// </summary>
public partial class GameManager : Node
{
	public enum GameState { MainMenu, Playing, Paused }

	public static GameManager? Instance { get; private set; }
	public GameState CurrentState { get; private set; } = GameState.MainMenu;

	/// <summary>
	/// The gameplay scene to load when the player presses Play.
	/// Set this before calling StartGame() to load a different prototype.
	/// </summary>
	public string GameScenePath { get; set; } =
		"res://Prototypes/P02_HoardAmalgamation/Scenes/P02Main.tscn";

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (CurrentState == GameState.Playing)
			{
				PauseGame();
				GetViewport().SetInputAsHandled();
			}
			else if (CurrentState == GameState.Paused)
			{
				ResumeGame();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public void StartGame()
	{
		CurrentState = GameState.Playing;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(GameScenePath);
	}

	public void PauseGame()
	{
		CurrentState = GameState.Paused;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Show the PauseMenu (spawned by the prototype's Main script)
		foreach (var node in GetTree().GetNodesInGroup("pause_menu"))
		{
			if (node is CanvasLayer cl)
			{
				cl.Visible = true;
				break;
			}
		}
	}

	public void ResumeGame()
	{
		CurrentState = GameState.Paused; // briefly, before we switch
		// Hide PauseMenu
		foreach (var node in GetTree().GetNodesInGroup("pause_menu"))
		{
			if (node is CanvasLayer cl)
			{
				cl.Visible = false;
				break;
			}
		}

		CurrentState = GameState.Playing;
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public void QuitToMainMenu()
	{
		CurrentState = GameState.MainMenu;
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GetTree().ChangeSceneToFile("res://Shared/Scenes/MainMenu.tscn");
	}

	public void QuitGame()
	{
		GetTree().Quit();
	}
}
