using Godot;

namespace Hoarders;

/// <summary>
/// First-person player controller with WASD movement, mouse look, jump, and sprint.
/// Attach to a CharacterBody3D with a "Head" Node3D child containing the Camera3D.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	[Export] public float WalkSpeed = 5.0f;
	[Export] public float SprintSpeed = 8.5f;
	[Export] public float JumpVelocity = 5.0f;
	[Export] public float MouseSensitivity = 0.002f;
	[Export] public float Acceleration = 10.0f;
	[Export] public float Friction = 8.0f;
	[Export] public int MaxHealth = 100;

	public bool InvertY = false;
	public int CurrentHealth { get; private set; }

	[Signal] public delegate void HealthChangedEventHandler(int current, int max);
	[Signal] public delegate void PlayerDiedEventHandler();

	private Node3D _head;
	private float _gravity;
	private bool _isDead = false;

	public override void _Ready()
	{
		AddToGroup("player");
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		_head = GetNode<Node3D>("Head");
		CurrentHealth = MaxHealth;
		Input.MouseMode = Input.MouseModeEnum.Captured;

		// Apply saved settings if SettingsManager is available
		var sm = GetNodeOrNull<Node>("/root/SettingsManager");
		if (sm != null)
		{
			MouseSensitivity = (float)sm.Get("MouseSensitivity");
			InvertY = (bool)sm.Get("InvertY");
			var cam = _head.GetNodeOrNull<Camera3D>("Camera3D");
			if (cam != null)
				cam.Fov = (float)sm.Get("FieldOfView");
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			float yFactor = InvertY ? 1.0f : -1.0f;
			_head.RotateX(mouseMotion.Relative.Y * MouseSensitivity * yFactor);
			var headRot = _head.Rotation;
			headRot.X = Mathf.Clamp(headRot.X, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
			_head.Rotation = headRot;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		var velocity = Velocity;

		// Gravity
		if (!IsOnFloor())
			velocity.Y -= _gravity * dt;

		// Jump
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
			velocity.Y = JumpVelocity;

		// Movement
		var inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		var direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;

		if (direction != Vector3.Zero)
		{
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, Acceleration * dt * speed);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, Acceleration * dt * speed);
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * dt * speed);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * dt * speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	public void TakeDamage(int amount)
	{
		if (_isDead) return;
		CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		if (CurrentHealth <= 0)
		{
			_isDead = true;
			EmitSignal(SignalName.PlayerDied);
		}
	}
}
