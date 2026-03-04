using Godot;

namespace Hoarders;

/// <summary>
/// Autoload that loads, saves, and applies user settings.
/// Persists to user://settings.cfg via Godot's ConfigFile.
/// </summary>
public partial class SettingsManager : Node
{
	public static SettingsManager? Instance { get; private set; }

	private const string SavePath = "user://settings.cfg";

	// --- Settings with sensible FPS defaults ---
	public float MouseSensitivity { get; private set; } = 0.002f;
	public float FieldOfView { get; private set; } = 80.0f;
	public float MasterVolume { get; private set; } = 1.0f;
	public bool InvertY { get; private set; } = false;
	public bool ScreenshakeEnabled { get; private set; } = true;

	private readonly ConfigFile _config = new();

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		Load();
		ApplyAudio();
	}

	/// <summary>
	/// Called by SettingsMenu when the player hits Apply.
	/// Stores values, applies them to live nodes, and saves to disk.
	/// </summary>
	public void Apply(float sensitivity, float fov, float masterVolume, bool invertY, bool screenshake)
	{
		MouseSensitivity = sensitivity;
		FieldOfView = fov;
		MasterVolume = masterVolume;
		InvertY = invertY;
		ScreenshakeEnabled = screenshake;
		ApplyAll();
		Save();
	}

	/// <summary>
	/// Pushes current settings into live gameplay nodes.
	/// Safe to call when no player exists (group will be empty).
	/// </summary>
	public void ApplyAll()
	{
		ApplyAudio();

		foreach (var node in GetTree().GetNodesInGroup("player"))
		{
			if (node is PlayerController pc)
			{
				pc.MouseSensitivity = MouseSensitivity;
				pc.InvertY = InvertY;
			}

			var cam = node.FindChild("Camera3D", true, false) as Camera3D;
			if (cam != null)
				cam.Fov = FieldOfView;
		}
	}

	private void ApplyAudio()
	{
		int busIdx = AudioServer.GetBusIndex("Master");
		if (busIdx >= 0)
		{
			float db = Mathf.LinearToDb(Mathf.Max(MasterVolume, 0.0001f));
			AudioServer.SetBusVolumeDb(busIdx, db);
		}
	}

	private void Save()
	{
		_config.SetValue("controls", "mouse_sensitivity", MouseSensitivity);
		_config.SetValue("controls", "invert_y", InvertY);
		_config.SetValue("display", "fov", FieldOfView);
		_config.SetValue("audio", "master_volume", MasterVolume);
		_config.SetValue("gameplay", "screenshake", ScreenshakeEnabled);
		_config.Save(SavePath);
	}

	private void Load()
	{
		if (_config.Load(SavePath) != Error.Ok)
			return; // first run — use defaults

		MouseSensitivity = (float)_config.GetValue("controls", "mouse_sensitivity", MouseSensitivity);
		InvertY = (bool)_config.GetValue("controls", "invert_y", InvertY);
		FieldOfView = (float)_config.GetValue("display", "fov", FieldOfView);
		MasterVolume = (float)_config.GetValue("audio", "master_volume", MasterVolume);
		ScreenshakeEnabled = (bool)_config.GetValue("gameplay", "screenshake", ScreenshakeEnabled);
	}
}
