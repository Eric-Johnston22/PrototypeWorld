using Godot;

namespace Hoarders;

/// <summary>
/// The Horde Amalgamation — a living pile of garbage that IS the room's enemy.
/// Drains health when the player's vacuum is pointed at it. Spawns debris chunks
/// as it takes damage. Aggros and shuffles toward the player when close enough.
/// </summary>
public partial class HordeAmalgamation : CharacterBody3D
{
	[Export] public int MaxHealth = 20;
	[Export] public float SuctionDamagePerSecond = 3.0f;
	[Export] public float MoveSpeed = 1.5f;
	[Export] public float AggroDistance = 6.0f;
	[Export] public float VacuumRange = 14.0f;

	private float _health;
	private float _nextChunkThreshold; // spawn a chunk each time health drops past a whole number

	private Vacuum? _vacuum;
	private CharacterBody3D? _player;
	private Label3D? _healthLabel;
	private AnimationPlayer? _animPlayer;
	private float _gravity;

	private enum State { Idle, Aggro, Dead }
	private State _state = State.Idle;

	private readonly RandomNumberGenerator _rng = new();

	[Signal] public delegate void DiedEventHandler();

	private static readonly Color[] DebrisColors =
	{
		new(0.55f, 0.45f, 0.3f),
		new(0.6f, 0.55f, 0.2f),
		new(0.35f, 0.5f, 0.3f),
		new(0.7f, 0.4f, 0.3f),
		new(0.5f, 0.5f, 0.5f),
		new(0.65f, 0.35f, 0.2f),
	};

	public override void _Ready()
	{
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
		_health = MaxHealth;
		_nextChunkThreshold = MaxHealth - 1;

		// Find vacuum and player via their groups (set in Vacuum._Ready / PlayerController._Ready)
		var vacuumNodes = GetTree().GetNodesInGroup("vacuum");
		if (vacuumNodes.Count > 0) _vacuum = vacuumNodes[0] as Vacuum;

		var playerNodes = GetTree().GetNodesInGroup("player");
		if (playerNodes.Count > 0) _player = playerNodes[0] as CharacterBody3D;

		// Floating health label above the monster
		_healthLabel = new Label3D();
		_healthLabel.Position = new Vector3(0, 3.4f, 0);
		_healthLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		_healthLabel.FontSize = 48;
		_healthLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		_healthLabel.OutlineSize = 8;
		_healthLabel.OutlineModulate = new Color(0, 0, 0, 0.8f);
		AddChild(_healthLabel);
		UpdateHealthLabel();

		// Try to find and play the first available animation from the GLB
		_animPlayer = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animPlayer != null)
		{
			var anims = _animPlayer.GetAnimationList();
			if (anims.Length > 0)
				_animPlayer.Play(anims[0]);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == State.Dead) return;

		float dt = (float)delta;
		var velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= _gravity * dt;

		// Check if vacuum is actively sucking and pointing at us
		if (_vacuum != null && _vacuum.IsSucking)
		{
			var toThis = GlobalPosition - _vacuum.NozzleGlobalPosition;
			float dist = toThis.Length();
			float angle = Mathf.RadToDeg(_vacuum.NozzleForward.AngleTo(toThis.Normalized()));

			// Use a slightly wider cone than regular objects — the amalgamation is a big target
			if (dist < VacuumRange && angle < _vacuum.SuctionConeAngle * 1.4f)
				TakeDamage(SuctionDamagePerSecond * dt);
		}

		// State machine
		if (_player != null)
		{
			float distToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);

			if (_state == State.Idle && distToPlayer < AggroDistance)
				EnterAggro();

			if (_state == State.Aggro)
			{
				var dir = new Vector3(
					_player.GlobalPosition.X - GlobalPosition.X,
					0,
					_player.GlobalPosition.Z - GlobalPosition.Z
				).Normalized();

				velocity.X = dir.X * MoveSpeed;
				velocity.Z = dir.Z * MoveSpeed;

				// Face player
				var lookTarget = new Vector3(_player.GlobalPosition.X, GlobalPosition.Y, _player.GlobalPosition.Z);
				if (GlobalPosition.DistanceTo(lookTarget) > 0.1f)
					LookAt(lookTarget, Vector3.Up);
			}
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private void TakeDamage(float amount)
	{
		if (_state == State.Dead) return;

		_health = Mathf.Max(0.0f, _health - amount);

		// Spawn a debris chunk for each whole health unit lost
		while (_health <= _nextChunkThreshold && _nextChunkThreshold >= 0)
		{
			SpawnDebrisChunk();
			_nextChunkThreshold -= 1.0f;
		}

		UpdateHealthLabel();

		// Brief scale pulse for feedback
		var tween = CreateTween();
		tween.TweenProperty(this, "scale", Vector3.One * 1.06f, 0.04f);
		tween.TweenProperty(this, "scale", Vector3.One, 0.1f);

		if (_health <= 0)
			Die();
	}

	private void EnterAggro()
	{
		_state = State.Aggro;

		// Try to switch to a walk/move animation if the GLB has one
		if (_animPlayer != null)
		{
			foreach (var anim in _animPlayer.GetAnimationList())
			{
				string name = anim.ToString().ToLower();
				if (name.Contains("walk") || name.Contains("move") || name.Contains("run"))
				{
					_animPlayer.Play(anim);
					return;
				}
			}
		}
	}

	private void SpawnDebrisChunk()
	{
		var chunk = new VacuumableObject();
		chunk.Size = VacuumableObject.ObjectSize.Small;
		chunk.DisplayName = "Junk Chunk";
		chunk.ObjectColor = DebrisColors[_rng.RandiRange(0, DebrisColors.Length - 1)];

		float s = _rng.RandfRange(0.1f, 0.28f);
		var mesh = new MeshInstance3D();
		var box = new BoxMesh();
		box.Size = new Vector3(s * 1.2f, s, s * 0.8f);
		mesh.Mesh = box;
		chunk.AddChild(mesh);

		var col = new CollisionShape3D();
		var shape = new BoxShape3D();
		shape.Size = box.Size;
		col.Shape = shape;
		chunk.AddChild(col);

		chunk.ContinuousCd = true;

		// Position near the monster, offset randomly — set before adding to tree
		// (Root transform is identity, so Position == GlobalPosition for root children)
		chunk.Position = GlobalPosition + new Vector3(
			_rng.RandfRange(-1.5f, 1.5f),
			_rng.RandfRange(0.5f, 2.5f),
			_rng.RandfRange(-1.5f, 1.5f)
		);
		chunk.Rotation = new Vector3(
			_rng.RandfRange(0, Mathf.Tau),
			_rng.RandfRange(0, Mathf.Tau),
			_rng.RandfRange(0, Mathf.Tau)
		);

		GetTree().Root.AddChild(chunk);

		// Explosive outward velocity
		chunk.LinearVelocity = new Vector3(
			_rng.RandfRange(-5f, 5f),
			_rng.RandfRange(3f, 8f),
			_rng.RandfRange(-5f, 5f)
		);
	}

	private void Die()
	{
		_state = State.Dead;

		// Death burst — shower of debris
		for (int i = 0; i < 15; i++)
			SpawnDebrisChunk();

		if (IsInstanceValid(_healthLabel))
			_healthLabel.QueueFree();

		EmitSignal(SignalName.Died);

		var timer = GetTree().CreateTimer(0.4);
		timer.Timeout += QueueFree;
	}

	private void UpdateHealthLabel()
	{
		if (!IsInstanceValid(_healthLabel)) return;
		int displayHealth = Mathf.Max(0, (int)Mathf.Ceil(_health));
		_healthLabel.Text = $"HP {displayHealth}/{MaxHealth}";
	}
}
