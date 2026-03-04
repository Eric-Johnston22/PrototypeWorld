using Godot;

namespace Hoarders;

/// <summary>
/// Pause overlay — RESUME, SETTINGS, QUIT TO MENU.
/// Spawned by prototype Main scripts. Starts hidden; GameManager shows/hides it.
/// ProcessMode is WhenPaused so it stays interactive while the tree is frozen.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
	private Control? _pausePanel;
	private SettingsMenu? _settingsPanel;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.WhenPaused;
		AddToGroup("pause_menu");
		Visible = false; // hidden until GameManager.PauseGame() shows us
		BuildUI();
	}

	private void BuildUI()
	{
		// Dark semi-transparent overlay
		var overlay = new ColorRect();
		overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0, 0, 0, 0.6f);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop; // block clicks through
		AddChild(overlay);

		// === Pause panel (title + buttons) ===
		_pausePanel = new Control();
		_pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_pausePanel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 18);
		vbox.CustomMinimumSize = new Vector2(300, 0);
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		vbox.Position = new Vector2(-150, -130);
		_pausePanel.AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = "PAUSED";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 0.9f));
		title.AddThemeFontSizeOverride("font_size", 40);
		vbox.AddChild(title);

		// Spacer
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 10);
		vbox.AddChild(spacer);

		// Resume
		var resumeBtn = MakeButton("RESUME");
		resumeBtn.Pressed += () => GameManager.Instance?.ResumeGame();
		vbox.AddChild(resumeBtn);

		// Settings
		var settingsBtn = MakeButton("SETTINGS");
		settingsBtn.Pressed += ShowSettings;
		vbox.AddChild(settingsBtn);

		// Quit to menu
		var quitBtn = MakeButton("QUIT TO MENU");
		quitBtn.Pressed += () => GameManager.Instance?.QuitToMainMenu();
		vbox.AddChild(quitBtn);

		// === Settings panel (starts hidden) ===
		_settingsPanel = new SettingsMenu();
		_settingsPanel.Visible = false;
		_settingsPanel.ProcessMode = ProcessModeEnum.WhenPaused;
		_settingsPanel.OnClose += ShowPause;
		AddChild(_settingsPanel);
	}

	private void ShowSettings()
	{
		_pausePanel!.Visible = false;
		_settingsPanel!.Visible = true;
		_settingsPanel.Refresh();
	}

	private void ShowPause()
	{
		_settingsPanel!.Visible = false;
		_pausePanel!.Visible = true;
	}

	private static Button MakeButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(300, 48);

		var normal = new StyleBoxFlat();
		normal.BgColor = new Color(0.12f, 0.1f, 0.1f, 0.9f);
		normal.BorderColor = new Color(0.5f, 0.4f, 0.3f);
		normal.SetBorderWidthAll(2);
		normal.SetCornerRadiusAll(6);
		normal.ContentMarginTop = 8;
		normal.ContentMarginBottom = 8;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = new StyleBoxFlat();
		hover.BgColor = new Color(0.2f, 0.15f, 0.12f, 0.95f);
		hover.BorderColor = new Color(0.7f, 0.5f, 0.3f);
		hover.SetBorderWidthAll(2);
		hover.SetCornerRadiusAll(6);
		hover.ContentMarginTop = 8;
		hover.ContentMarginBottom = 8;
		btn.AddThemeStyleboxOverride("hover", hover);

		var pressed = new StyleBoxFlat();
		pressed.BgColor = new Color(0.28f, 0.18f, 0.12f, 0.95f);
		pressed.BorderColor = new Color(1.0f, 0.6f, 0.2f);
		pressed.SetBorderWidthAll(2);
		pressed.SetCornerRadiusAll(6);
		pressed.ContentMarginTop = 8;
		pressed.ContentMarginBottom = 8;
		btn.AddThemeStyleboxOverride("pressed", pressed);

		btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.7f));
		btn.AddThemeFontSizeOverride("font_size", 20);
		return btn;
	}
}
