using Godot;

namespace Hoarders;

/// <summary>
/// The Hoard Amalgamation — a living pile of garbage that IS the room's enemy.
/// Uses an animation-driven state machine with four animations from the GLB:
///   idle        — default, plays when out of aggro range
///   throw       — ranged attack; SpawnProjectile() fires at ThrowReleaseTime
///   hand attack — melee attack when player is very close; debris burst at MeleeImpactTime
///   hurt        — interrupts any state when taking vacuum damage, then resumes
/// </summary>
public partial class HoardAmalgamation : CharacterBody3D
{
	[Export] public int MaxHealth = 20;
	[Export] public float SuctionDamagePerSecond = 3.0f;
	[Export] public float MoveSpeed = 1.5f;
	[Export] public float AggroDistance = 9.0f;   // ~10 yards
	[Export] public float MeleeDistance = 2.7f;   // ~3 yards
	[Export] public float VacuumRange = 14.0f;
	[Export] public float ProjectileSpeed = 12.0f;
	[Export] public int MeleeDamage = 15;

	// Tune these to match the exact keyframe moment in the GLB animations
	[Export] public float ThrowReleaseTime = 0.6f;  // seconds into "throw" when projectile spawns
	[Export] public float MeleeImpactTime = 0.4f;   // seconds into "hand attack" when damage lands
	[Export] public float ThrowCooldown = 2.5f;     // seconds between throws
	[Export] public float HurtCooldown = 0.5f;      // prevents hurt anim spamming every frame

	private float _health;
	private float _nextChunkThreshold;

	private Vacuum? _vacuum;
	private CharacterBody3D? _player;
	private Label3D? _healthLabel;
	private AnimationPlayer? _animPlayer;
	private float _gravity;

	private enum State { Idle, ThrowAttack, MeleeAttack, Hurt, Dead }
	private State _state = State.Idle;
	private State _stateBeforeHurt = State.Idle;

	private bool _throwEventFired = false;
	private bool _meleeEventFired = false;
	private float _throwCooldownTimer = 0f;
	private float _hurtCooldownTimer = 0f;

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

		var vacuumNodes = GetTree().GetNodesInGroup("vacuum");
		if (vacuumNodes.Count > 0) _vacuum = vacuumNodes[0] as Vacuum;

		var playerNodes = GetTree().GetNodesInGroup("player");
		if (playerNodes.Count > 0) _player = playerNodes[0] as CharacterBody3D;

		// Floating health label
		_healthLabel = new Label3D();
		_healthLabel.Position = new Vector3(0, 3.4f, 0);
		_healthLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		_healthLabel.FontSize = 48;
		_healthLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		_healthLabel.OutlineSize = 8;
		_healthLabel.OutlineModulate = new Color(0, 0, 0, 0.8f);
		AddChild(_healthLabel);
		UpdateHealthLabel();

		_animPlayer = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (_animPlayer != null)
			_animPlayer.AnimationFinished += OnAnimationFinished;

		PlayAnim("Idle", loop: true);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == State.Dead) return;

		float dt = (float)delta;
		var velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= _gravity * dt;

		// Cooldown timers
		_throwCooldownTimer = Mathf.Max(0f, _throwCooldownTimer - dt);
		_hurtCooldownTimer = Mathf.Max(0f, _hurtCooldownTimer - dt);

		// Vacuum damage check
		if (_vacuum != null && _vacuum.IsSucking)
		{
			var toThis = GlobalPosition - _vacuum.NozzleGlobalPosition;
			float dist = toThis.Length();
			float angle = Mathf.RadToDeg(_vacuum.NozzleForward.AngleTo(toThis.Normalized()));
			if (dist < VacuumRange && angle < _vacuum.SuctionConeAngle * 1.4f)
				TakeDamage(SuctionDamagePerSecond * dt);
		}

		// Don't change state or move while hurt animation plays
		if (_state == State.Hurt)
		{
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		// Determine desired state based on player distance
		if (_player != null)
		{
			float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

			if (dist > AggroDistance)
				TransitionTo(State.Idle);
			else if (dist <= MeleeDistance)
				TransitionTo(State.MeleeAttack);
			else
				TransitionTo(State.ThrowAttack);

			if (_state != State.Idle)
				FacePlayer();
		}

		// Per-state per-frame behavior
		switch (_state)
		{
			case State.ThrowAttack:
				// Check if we've reached the release frame yet
				if (!_throwEventFired
					&& _animPlayer != null
					&& _animPlayer.CurrentAnimationPosition >= ThrowReleaseTime)
				{
					_throwEventFired = true;
					SpawnProjectile();
				}
				break;

			case State.MeleeAttack:
				// Lunge toward player during the windup portion
				if (_player != null
					&& _animPlayer != null
					&& _animPlayer.CurrentAnimationPosition < MeleeImpactTime)
				{
					var dir = (_player.GlobalPosition - GlobalPosition with { Y = GlobalPosition.Y }).Normalized();
					velocity.X = dir.X * MoveSpeed * 2.5f;
					velocity.Z = dir.Z * MoveSpeed * 2.5f;
				}

				// Fire impact event at the right frame
				if (!_meleeEventFired
					&& _animPlayer != null
					&& _animPlayer.CurrentAnimationPosition >= MeleeImpactTime)
				{
					_meleeEventFired = true;
					OnMeleeImpact();
				}
				break;
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// -------------------------------------------------------------------------
	// State machine
	// -------------------------------------------------------------------------

	private void TransitionTo(State newState)
	{
		if (_state == newState) return;

		_state = newState;

		switch (newState)
		{
			case State.Idle:
				PlayAnim("Idle", loop: true);
				break;

			case State.ThrowAttack:
				if (_throwCooldownTimer <= 0f)
				{
					_throwEventFired = false;
					PlayAnim("throw");
				}
				else
				{
					// Cooldown still running — idle in place until ready
					PlayAnim("Idle", loop: true);
					_state = State.Idle; // don't commit to ThrowAttack yet
				}
				break;

			case State.MeleeAttack:
				_meleeEventFired = false;
				PlayAnim("hand attack");
				break;
		}
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (_state == State.Dead) return;

		if (animName == "throw")
		{
			_throwCooldownTimer = ThrowCooldown;
			// Re-evaluate next physics frame — fall back to idle so TransitionTo works
			_state = State.Idle;
			PlayAnim("Idle", loop: true);
		}
		else if (animName == "hand attack")
		{
			_state = State.Idle;
			PlayAnim("Idle", loop: true);
		}
		else if (animName == "hurt")
		{
			// Resume the state we interrupted
			_state = _stateBeforeHurt;
			switch (_state)
			{
				case State.Idle:
					PlayAnim("Idle", loop: true);
					break;
				case State.ThrowAttack:
					_throwEventFired = false;
					PlayAnim("throw");
					break;
				case State.MeleeAttack:
					_meleeEventFired = false;
					PlayAnim("hand attack");
					break;
				default:
					PlayAnim("Idle", loop: true);
					break;
			}
		}
	}

	// -------------------------------------------------------------------------
	// Combat events
	// -------------------------------------------------------------------------

	private void SpawnProjectile()
	{
		var chunk = new VacuumableObject();
		chunk.Size = VacuumableObject.ObjectSize.Small;
		chunk.DisplayName = "Thrown Junk";
		chunk.ObjectColor = DebrisColors[_rng.RandiRange(0, DebrisColors.Length - 1)];

		float s = _rng.RandfRange(0.15f, 0.3f);
		var mesh = new MeshInstance3D();
		mesh.Mesh = new BoxMesh { Size = new Vector3(s, s, s) };
		chunk.AddChild(mesh);

		var col = new CollisionShape3D();
		col.Shape = new BoxShape3D { Size = new Vector3(s, s, s) };
		chunk.AddChild(col);

		chunk.ContinuousCd = true;
		chunk.Position = GlobalPosition + Vector3.Up * 1.8f;
		GetTree().Root.AddChild(chunk);

		if (_player != null)
		{
			var target = _player.GlobalPosition + Vector3.Up * 1.0f;
			chunk.LinearVelocity = (target - chunk.Position).Normalized() * ProjectileSpeed;
		}
	}

	private void OnMeleeImpact()
	{
		// Visual feedback — debris burst at impact
		for (int i = 0; i < 4; i++)
			SpawnDebrisChunk();

		// Damage the player
		if (_player is PlayerController pc)
			pc.TakeDamage(MeleeDamage);
	}

	private void TakeDamage(float amount)
	{
		if (_state == State.Dead) return;

		_health = Mathf.Max(0.0f, _health - amount);

		while (_health <= _nextChunkThreshold && _nextChunkThreshold >= 0)
		{
			SpawnDebrisChunk();
			_nextChunkThreshold -= 1.0f;
		}

		UpdateHealthLabel();

		// Play hurt animation (with cooldown so it doesn't fire every physics frame)
		if (_hurtCooldownTimer <= 0f && _state != State.Hurt)
		{
			_stateBeforeHurt = _state;
			_state = State.Hurt;
			_hurtCooldownTimer = HurtCooldown;
			PlayAnim("hurt");
		}
		else
		{
			// Hurt anim on cooldown — just do a scale pulse for feedback
			var tween = CreateTween();
			tween.TweenProperty(this, "scale", Vector3.One * 1.06f, 0.04f);
			tween.TweenProperty(this, "scale", Vector3.One, 0.1f);
		}

		if (_health <= 0)
			Die();
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private void PlayAnim(string name, bool loop = false)
	{
		if (_animPlayer == null) return;
		if (!_animPlayer.HasAnimation(name))
		{
			GD.PrintErr($"HoardAmalgamation: animation '{name}' not found. Available: {string.Join(", ", _animPlayer.GetAnimationList())}");
			return;
		}

		var anim = _animPlayer.GetAnimation(name);
		anim.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
		_animPlayer.Play(name);
	}

	private void FacePlayer()
	{
		if (_player == null) return;
		var lookTarget = new Vector3(_player.GlobalPosition.X, GlobalPosition.Y, _player.GlobalPosition.Z);
		if (GlobalPosition.DistanceTo(lookTarget) > 0.1f)
			LookAt(lookTarget, Vector3.Up);
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

		chunk.LinearVelocity = new Vector3(
			_rng.RandfRange(-5f, 5f),
			_rng.RandfRange(3f, 8f),
			_rng.RandfRange(-5f, 5f)
		);
	}

	private void Die()
	{
		_state = State.Dead;

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
