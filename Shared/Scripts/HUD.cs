using Godot;

namespace Hoarders;

/// <summary>
/// In-game HUD showing crosshair, vacuum capacity, collected item notifications,
/// and vacuum state indicator. Attach to a CanvasLayer.
/// </summary>
public partial class HUD : CanvasLayer
{
	private Label _itemCountLabel;
	private ProgressBar _capacityBar;
	private Label _notificationLabel;
	private Label _stateLabel;
	private ColorRect _crosshairH;
	private ColorRect _crosshairV;
	private ColorRect _crosshairDot;
	private Control _crosshairContainer;
	private float _notificationTimer;
	private int _maxCapacity = 100;

	private ProgressBar _healthBar;
	private Label _healthLabel;

	// Crosshair animation
	private float _crosshairSpread;
	private float _targetSpread;

	public override void _Ready()
	{
		BuildUI();
	}

	private void BuildUI()
	{
		// Root control
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		// --- Crosshair ---
		_crosshairContainer = new Control();
		_crosshairContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		_crosshairContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
		root.AddChild(_crosshairContainer);

		var crosshairColor = new Color(1, 1, 1, 0.7f);

		_crosshairDot = new ColorRect();
		_crosshairDot.Size = new Vector2(4, 4);
		_crosshairDot.Position = new Vector2(-2, -2);
		_crosshairDot.Color = crosshairColor;
		_crosshairDot.MouseFilter = Control.MouseFilterEnum.Ignore;
		_crosshairContainer.AddChild(_crosshairDot);

		_crosshairH = new ColorRect();
		_crosshairH.Size = new Vector2(20, 2);
		_crosshairH.Position = new Vector2(-10, -1);
		_crosshairH.Color = crosshairColor;
		_crosshairH.MouseFilter = Control.MouseFilterEnum.Ignore;
		_crosshairContainer.AddChild(_crosshairH);

		_crosshairV = new ColorRect();
		_crosshairV.Size = new Vector2(2, 20);
		_crosshairV.Position = new Vector2(-1, -10);
		_crosshairV.Color = crosshairColor;
		_crosshairV.MouseFilter = Control.MouseFilterEnum.Ignore;
		_crosshairContainer.AddChild(_crosshairV);

		// --- Bottom-right panel: vacuum info ---
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		panel.Position = new Vector2(-280, -120);
		panel.Size = new Vector2(260, 100);
		panel.MouseFilter = Control.MouseFilterEnum.Ignore;
		root.AddChild(panel);

		// Style the panel
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0, 0, 0, 0.5f);
		panelStyle.CornerRadiusTopLeft = 8;
		panelStyle.CornerRadiusTopRight = 8;
		panelStyle.CornerRadiusBottomLeft = 8;
		panelStyle.CornerRadiusBottomRight = 8;
		panelStyle.ContentMarginLeft = 12;
		panelStyle.ContentMarginRight = 12;
		panelStyle.ContentMarginTop = 8;
		panelStyle.ContentMarginBottom = 8;
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		panel.AddChild(vbox);

		// Vacuum title
		var titleLabel = new Label();
		titleLabel.Text = "SUCK-O-MATIC 3000";
		titleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1.0f));
		titleLabel.AddThemeFontSizeOverride("font_size", 14);
		vbox.AddChild(titleLabel);

		// Item count
		_itemCountLabel = new Label();
		_itemCountLabel.Text = "Collected: 0";
		_itemCountLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
		_itemCountLabel.AddThemeFontSizeOverride("font_size", 16);
		vbox.AddChild(_itemCountLabel);

		// Capacity bar
		_capacityBar = new ProgressBar();
		_capacityBar.MinValue = 0;
		_capacityBar.MaxValue = 100;
		_capacityBar.Value = 0;
		_capacityBar.ShowPercentage = false;
		_capacityBar.CustomMinimumSize = new Vector2(0, 18);

		var barBg = new StyleBoxFlat();
		barBg.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
		barBg.CornerRadiusTopLeft = 4;
		barBg.CornerRadiusTopRight = 4;
		barBg.CornerRadiusBottomLeft = 4;
		barBg.CornerRadiusBottomRight = 4;
		_capacityBar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor = new Color(0.3f, 0.85f, 0.4f);
		barFill.CornerRadiusTopLeft = 4;
		barFill.CornerRadiusTopRight = 4;
		barFill.CornerRadiusBottomLeft = 4;
		barFill.CornerRadiusBottomRight = 4;
		_capacityBar.AddThemeStyleboxOverride("fill", barFill);

		vbox.AddChild(_capacityBar);

		// State indicator
		_stateLabel = new Label();
		_stateLabel.Text = "";
		_stateLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.5f, 0.8f));
		_stateLabel.AddThemeFontSizeOverride("font_size", 12);
		vbox.AddChild(_stateLabel);

		// --- Center notification (floats up and fades) ---
		_notificationLabel = new Label();
		_notificationLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_notificationLabel.Position = new Vector2(-150, 40);
		_notificationLabel.Size = new Vector2(300, 30);
		_notificationLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_notificationLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		_notificationLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0));
		_notificationLabel.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(_notificationLabel);

		// --- Bottom-left panel: player health ---
		var hpPanel = new PanelContainer();
		hpPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		hpPanel.Position = new Vector2(20, -110);
		hpPanel.Size = new Vector2(220, 80);
		hpPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
		hpPanel.AddThemeStyleboxOverride("panel", panelStyle);
		root.AddChild(hpPanel);

		var hpVbox = new VBoxContainer();
		hpVbox.AddThemeConstantOverride("separation", 4);
		hpPanel.AddChild(hpVbox);

		_healthLabel = new Label();
		_healthLabel.Text = "HP  100 / 100";
		_healthLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.35f));
		_healthLabel.AddThemeFontSizeOverride("font_size", 14);
		hpVbox.AddChild(_healthLabel);

		_healthBar = new ProgressBar();
		_healthBar.MinValue = 0;
		_healthBar.MaxValue = 100;
		_healthBar.Value = 100;
		_healthBar.ShowPercentage = false;
		_healthBar.CustomMinimumSize = new Vector2(0, 22);

		var hpBg = new StyleBoxFlat();
		hpBg.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
		hpBg.SetCornerRadiusAll(4);
		_healthBar.AddThemeStyleboxOverride("background", hpBg);

		var hpFill = new StyleBoxFlat();
		hpFill.BgColor = new Color(0.85f, 0.2f, 0.2f);
		hpFill.SetCornerRadiusAll(4);
		_healthBar.AddThemeStyleboxOverride("fill", hpFill);

		hpVbox.AddChild(_healthBar);

		// --- Controls hint (top-left) ---
		var hintsLabel = new Label();
		hintsLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		hintsLabel.Position = new Vector2(16, 16);
		hintsLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		hintsLabel.Text = "WASD - Move | Shift - Sprint | Space - Jump\nLeft Click - Suck | Right Click - Blow | Esc - Pause";
		hintsLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
		hintsLabel.AddThemeFontSizeOverride("font_size", 12);
		root.AddChild(hintsLabel);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// Fade out notification
		if (_notificationTimer > 0)
		{
			_notificationTimer -= dt;
			float alpha = Mathf.Clamp(_notificationTimer / 1.5f, 0, 1);
			_notificationLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, alpha));

			// Float upward
			var pos = _notificationLabel.Position;
			pos.Y -= 15.0f * dt;
			_notificationLabel.Position = pos;
		}

		// Animate crosshair spread
		_crosshairSpread = Mathf.Lerp(_crosshairSpread, _targetSpread, dt * 10.0f);
		float spread = _crosshairSpread;

		_crosshairH.Position = new Vector2(-10 - spread, -1);
		_crosshairH.Size = new Vector2(20 + spread * 2, 2);
		_crosshairV.Position = new Vector2(-1, -10 - spread);
		_crosshairV.Size = new Vector2(2, 20 + spread * 2);

		_targetSpread = 0;
	}

	public void OnItemCollected(int totalCollected, int maxCapacity, string objectName)
	{
		_maxCapacity = maxCapacity;
		_itemCountLabel.Text = $"Collected: {totalCollected}";
		_capacityBar.MaxValue = maxCapacity;
		_capacityBar.Value = totalCollected;

		// Update bar color based on fullness
		float ratio = (float)totalCollected / maxCapacity;
		var fillStyle = _capacityBar.GetThemeStylebox("fill") as StyleBoxFlat;
		if (fillStyle != null)
		{
			if (ratio > 0.85f)
				fillStyle.BgColor = new Color(0.9f, 0.2f, 0.2f);
			else if (ratio > 0.6f)
				fillStyle.BgColor = new Color(0.9f, 0.7f, 0.2f);
			else
				fillStyle.BgColor = new Color(0.3f, 0.85f, 0.4f);
		}

		// Show notification
		_notificationLabel.Text = $"Sucked up: {objectName}!";
		_notificationLabel.Position = new Vector2(
			_notificationLabel.Position.X, 40);
		_notificationTimer = 2.0f;
	}

	public void OnHealthChanged(int current, int max)
	{
		_healthBar.MaxValue = max;
		_healthBar.Value = current;
		_healthLabel.Text = $"HP  {current} / {max}";

		// Bar turns darker red as health drops
		float ratio = (float)current / max;
		var fill = _healthBar.GetThemeStylebox("fill") as StyleBoxFlat;
		if (fill != null)
			fill.BgColor = ratio > 0.5f
				? new Color(0.85f, 0.2f, 0.2f)
				: new Color(0.6f, 0.08f, 0.08f);
	}

	public void OnVacuumStateChanged(bool sucking, bool blowing)
	{
		if (sucking)
		{
			_stateLabel.Text = "[ SUCKING ]";
			_stateLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.5f, 0.9f));
			_targetSpread = 6.0f;

			// Animate crosshair color
			var suckColor = new Color(0.5f, 1.0f, 0.6f, 0.9f);
			_crosshairDot.Color = suckColor;
			_crosshairH.Color = suckColor;
			_crosshairV.Color = suckColor;
		}
		else if (blowing)
		{
			_stateLabel.Text = "[ BLOWING ]";
			_stateLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.6f, 0.3f, 0.9f));
			_targetSpread = 8.0f;

			var blowColor = new Color(1.0f, 0.7f, 0.3f, 0.9f);
			_crosshairDot.Color = blowColor;
			_crosshairH.Color = blowColor;
			_crosshairV.Color = blowColor;
		}
		else
		{
			_stateLabel.Text = "";
			var defaultColor = new Color(1, 1, 1, 0.7f);
			_crosshairDot.Color = defaultColor;
			_crosshairH.Color = defaultColor;
			_crosshairV.Color = defaultColor;
		}
	}
}
