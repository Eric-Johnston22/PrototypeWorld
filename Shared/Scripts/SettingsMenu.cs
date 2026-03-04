using Godot;

namespace Hoarders;

/// <summary>
/// Settings panel embedded inside MainMenu and PauseMenu.
/// This is a Control, not a CanvasLayer — the parent owns the layer.
/// </summary>
public partial class SettingsMenu : Control
{
	[Signal] public delegate void OnCloseEventHandler();

	private HSlider? _sensitivitySlider;
	private HSlider? _fovSlider;
	private HSlider? _volumeSlider;
	private CheckButton? _invertYCheck;
	private CheckButton? _screenshakeCheck;
	private Label? _sensitivityValue;
	private Label? _fovValue;
	private Label? _volumeValue;

	public override void _Ready()
	{
		BuildUI();
	}

	/// <summary>
	/// Sync slider positions to current live settings. Call before showing.
	/// </summary>
	public void Refresh()
	{
		var s = SettingsManager.Instance;
		if (s == null) return;

		_sensitivitySlider!.Value = s.MouseSensitivity * 1000.0;
		_fovSlider!.Value = s.FieldOfView;
		_volumeSlider!.Value = s.MasterVolume * 100.0;
		_invertYCheck!.ButtonPressed = s.InvertY;
		_screenshakeCheck!.ButtonPressed = s.ScreenshakeEnabled;
		UpdateLabels();
	}

	private void BuildUI()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// Semi-transparent panel background
		var bg = new ColorRect();
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		bg.Color = new Color(0.05f, 0.03f, 0.03f, 0.85f);
		bg.MouseFilter = MouseFilterEnum.Stop;
		AddChild(bg);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		vbox.CustomMinimumSize = new Vector2(400, 0);
		vbox.Position = new Vector2(-200, -180);
		AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = "SETTINGS";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.2f));
		title.AddThemeFontSizeOverride("font_size", 32);
		vbox.AddChild(title);

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 8);
		vbox.AddChild(spacer);

		// Mouse Sensitivity (slider 1–10, maps to 0.001–0.010)
		(_sensitivitySlider, _sensitivityValue) = AddSliderRow(vbox, "Mouse Sensitivity", 1.0, 10.0, 0.1);

		// FOV (60–120)
		(_fovSlider, _fovValue) = AddSliderRow(vbox, "Field of View", 60.0, 120.0, 1.0);

		// Master Volume (0–100)
		(_volumeSlider, _volumeValue) = AddSliderRow(vbox, "Master Volume", 0.0, 100.0, 1.0);

		// Invert Y
		_invertYCheck = AddCheckRow(vbox, "Invert Y Axis");

		// Screenshake
		_screenshakeCheck = AddCheckRow(vbox, "Screen Shake");

		// Spacer before buttons
		var spacer2 = new Control();
		spacer2.CustomMinimumSize = new Vector2(0, 12);
		vbox.AddChild(spacer2);

		// Button row
		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(btnRow);

		var applyBtn = MakeButton("APPLY");
		applyBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		applyBtn.Pressed += OnApply;
		btnRow.AddChild(applyBtn);

		var backBtn = MakeButton("BACK");
		backBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		backBtn.Pressed += () => EmitSignal(SignalName.OnClose);
		btnRow.AddChild(backBtn);

		// Wire sliders to live-update value labels
		_sensitivitySlider.ValueChanged += _ => UpdateLabels();
		_fovSlider.ValueChanged += _ => UpdateLabels();
		_volumeSlider.ValueChanged += _ => UpdateLabels();
	}

	private void OnApply()
	{
		SettingsManager.Instance?.Apply(
			(float)_sensitivitySlider!.Value / 1000.0f,
			(float)_fovSlider!.Value,
			(float)_volumeSlider!.Value / 100.0f,
			_invertYCheck!.ButtonPressed,
			_screenshakeCheck!.ButtonPressed
		);
	}

	private void UpdateLabels()
	{
		_sensitivityValue!.Text = $"{_sensitivitySlider!.Value:F1}";
		_fovValue!.Text = $"{_fovSlider!.Value:F0}°";
		_volumeValue!.Text = $"{_volumeSlider!.Value:F0}%";
	}

	private static (HSlider slider, Label valueLabel) AddSliderRow(
		VBoxContainer parent, string labelText, double min, double max, double step)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var nameLabel = new Label();
		nameLabel.Text = labelText;
		nameLabel.CustomMinimumSize = new Vector2(160, 0);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.75f));
		row.AddChild(nameLabel);

		var slider = new HSlider();
		slider.MinValue = min;
		slider.MaxValue = max;
		slider.Step = step;
		slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		slider.CustomMinimumSize = new Vector2(150, 0);
		row.AddChild(slider);

		var valueLabel = new Label();
		valueLabel.CustomMinimumSize = new Vector2(55, 0);
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.75f));
		row.AddChild(valueLabel);

		parent.AddChild(row);
		return (slider, valueLabel);
	}

	private static CheckButton AddCheckRow(VBoxContainer parent, string labelText)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		var label = new Label();
		label.Text = labelText;
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.75f));
		row.AddChild(label);

		var check = new CheckButton();
		row.AddChild(check);

		parent.AddChild(row);
		return check;
	}

	private static Button MakeButton(string text)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(0, 40);

		var normal = new StyleBoxFlat();
		normal.BgColor = new Color(0.15f, 0.1f, 0.08f);
		normal.BorderColor = new Color(0.5f, 0.35f, 0.2f);
		normal.SetBorderWidthAll(2);
		normal.SetCornerRadiusAll(6);
		normal.ContentMarginTop = 6;
		normal.ContentMarginBottom = 6;
		normal.ContentMarginLeft = 12;
		normal.ContentMarginRight = 12;
		btn.AddThemeStyleboxOverride("normal", normal);

		var hover = new StyleBoxFlat();
		hover.BgColor = new Color(0.22f, 0.14f, 0.1f);
		hover.BorderColor = new Color(0.7f, 0.45f, 0.2f);
		hover.SetBorderWidthAll(2);
		hover.SetCornerRadiusAll(6);
		hover.ContentMarginTop = 6;
		hover.ContentMarginBottom = 6;
		hover.ContentMarginLeft = 12;
		hover.ContentMarginRight = 12;
		btn.AddThemeStyleboxOverride("hover", hover);

		btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.7f));
		btn.AddThemeFontSizeOverride("font_size", 18);
		return btn;
	}
}
