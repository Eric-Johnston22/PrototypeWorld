using Godot;

namespace Hoarders;

/// <summary>
/// Main menu screen — PLAY, SETTINGS, QUIT.
/// Built programmatically to match the existing HUD pattern.
/// </summary>
public partial class MainMenu : CanvasLayer
{
	private Control? _mainPanel;
	private SettingsMenu? _settingsPanel;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		BuildUI();
	}

	private void BuildUI()
	{
		// Full-screen dark background
		var bg = new ColorRect();
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color(0.06f, 0.04f, 0.04f, 1.0f);
		bg.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(bg);

		// === Main panel (title + buttons) ===
		_mainPanel = new Control();
		_mainPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_mainPanel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		vbox.CustomMinimumSize = new Vector2(320, 0);
		// Center it on screen
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		vbox.Position = new Vector2(-160, -160);
		_mainPanel.AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = "HOARDERS";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.2f));
		title.AddThemeFontSizeOverride("font_size", 52);
		vbox.AddChild(title);

		// Subtitle
		var subtitle = new Label();
		subtitle.Text = "Suck-O-Matic 3000 Edition";
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		subtitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.5f, 0.4f));
		subtitle.AddThemeFontSizeOverride("font_size", 16);
		vbox.AddChild(subtitle);

		// Spacer
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 24);
		vbox.AddChild(spacer);

		// Play button
		var playBtn = MakeButton("PLAY");
		playBtn.Pressed += () => GameManager.Instance?.StartGame();
		vbox.AddChild(playBtn);

		// Settings button
		var settingsBtn = MakeButton("SETTINGS");
		settingsBtn.Pressed += ShowSettings;
		vbox.AddChild(settingsBtn);

		// Quit button
		var quitBtn = MakeButton("QUIT");
		quitBtn.Pressed += () => GameManager.Instance?.QuitGame();
		vbox.AddChild(quitBtn);

		// === Settings panel (starts hidden) ===
		_settingsPanel = new SettingsMenu();
		_settingsPanel.Visible = false;
		_settingsPanel.OnClose += ShowMain;
		AddChild(_settingsPanel);
	}

	private void ShowSettings()
	{
		_mainPanel!.Visible = false;
		_settingsPanel!.Visible = true;
		_settingsPanel.Refresh();
	}

	private void ShowMain()
	{
		_settingsPanel!.Visible = false;
		_mainPanel!.Visible = true;
	}

	private static Button MakeButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(320, 52);

		var normal = new StyleBoxFlat();
		normal.BgColor = new Color(0.15f, 0.1f, 0.08f);
		normal.BorderColor = new Color(0.5f, 0.35f, 0.2f);
		normal.SetBorderWidthAll(2);
		normal.SetCornerRadiusAll(6);
		normal.ContentMarginTop = 10;
		normal.ContentMarginBottom = 10;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = new StyleBoxFlat();
		hover.BgColor = new Color(0.22f, 0.14f, 0.1f);
		hover.BorderColor = new Color(0.7f, 0.45f, 0.2f);
		hover.SetBorderWidthAll(2);
		hover.SetCornerRadiusAll(6);
		hover.ContentMarginTop = 10;
		hover.ContentMarginBottom = 10;
		btn.AddThemeStyleboxOverride("hover", hover);

		var pressed = new StyleBoxFlat();
		pressed.BgColor = new Color(0.3f, 0.18f, 0.1f);
		pressed.BorderColor = new Color(1.0f, 0.6f, 0.2f);
		pressed.SetBorderWidthAll(2);
		pressed.SetCornerRadiusAll(6);
		pressed.ContentMarginTop = 10;
		pressed.ContentMarginBottom = 10;
		btn.AddThemeStyleboxOverride("pressed", pressed);

		btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.7f));
		btn.AddThemeFontSizeOverride("font_size", 22);
		return btn;
	}
}
