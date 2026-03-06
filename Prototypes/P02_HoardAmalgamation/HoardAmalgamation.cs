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
	[Export] public int MaxHealth = 100;
	[Export] public float SuctionDamagePerSecond = 3.0f;
	[Export] public float MoveSpeed = 2.5f;
	[Export] public float AggroDistance = 20.0f;  // ~22 yards — active across most of the room
	[Export] public float MeleeDistance = 7.0f;   // ~3 yards
	[Export] public float VacuumRange = 14.0f;
	[Export] public float ProjectileSpeed = 18.0f;
	[Export] public int MeleeDamage = 15;
	[Export] public int ProjectileDamage = 10;
	// Local-space offset from monster origin → projectile spawn point (right hand).
	// X = right, Y = height, Z = forward (-Z faces player after LookAt).
	// Overridden automatically if a hand bone is found on the skeleton.
	[Export] public Vector3 ThrowOffset = new(0.8f, 1.8f, -0.3f);

	// Animation names — must match exactly what Godot imported from the GLB.
	// Check the Output panel on first run; available names are printed there.
	[Export] public string AnimIdle   = "Idle";
	[Export] public string AnimThrow  = "throw";
	[Export] public string AnimMelee  = "hand attack";
	[Export] public string AnimHurt   = "hurt";

	// Tune these to match the exact keyframe moment in the GLB animations
	[Export] public float ThrowReleaseTime = 0.6f;  // seconds into throw anim when projectile spawns
	[Export] public float MeleeImpactTime = 0.4f;   // seconds into melee anim when damage lands
	[Export] public float ThrowCooldown = 1.0f;     // seconds between throws
	[Export] public float HurtCooldown = 0.5f;      // prevents hurt anim spamming every frame

	private float _health;
	private float _nextChunkThreshold;

	private Vacuum? _vacuum;
	private CharacterBody3D? _player;
	private Label3D? _healthLabel;
	private AnimationPlayer? _animPlayer;
	private Skeleton3D? _skeleton;
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
		AddToGroup("hoard_amalgamation");

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
		{
			_animPlayer.AnimationFinished += OnAnimationFinished;
			GD.Print($"[HoardAmalgamation] Available animations: {string.Join(", ", _animPlayer.GetAnimationList())}");
		}
		else
		{
			GD.PrintErr("[HoardAmalgamation] No AnimationPlayer found!");
		}

		_skeleton = FindChild("Skeleton3D", true, false) as Skeleton3D;

		PlayAnim(AnimIdle, loop: true);
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

		// Determine desired state based on horizontal (XZ) distance to player.
		// Horizontal-only avoids false readings when one body has a different Y origin.
		if (_player != null)
		{
			var toPlayer = _player.GlobalPosition - GlobalPosition;
			toPlayer.Y = 0f;
			float dist = toPlayer.Length();

			if (dist > AggroDistance)
			{
				if (_state != State.Idle)
					TransitionTo(State.Idle);
			}
			else if (dist <= MeleeDistance)
			{
				// Melee takes full priority — interrupt throw if needed, but never hurt
				if (_state != State.MeleeAttack && _state != State.Hurt)
				{
					GD.Print($"[HoardAmalgamation] Entering MELEE at dist={dist:F2}m, prev state={_state}");
					TransitionTo(State.MeleeAttack);
				}
			}
			else
			{
				// Throw range — only start a new throw from Idle when cooldown allows
				if (_state == State.Idle && _throwCooldownTimer <= 0f)
					TransitionTo(State.ThrowAttack);
			}

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
				PlayAnim(AnimIdle, loop: true);
				break;

			case State.ThrowAttack:
				_throwEventFired = false;
				PlayAnim(AnimThrow);
				break;

			case State.MeleeAttack:
				_meleeEventFired = false;
				PlayAnim(AnimMelee);
				break;
		}
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (_state == State.Dead) return;

		GD.Print($"[HoardAmalgamation] AnimFinished: '{animName}'  state={_state}");

		if (animName == AnimThrow)
		{
			_throwCooldownTimer = ThrowCooldown;
			_state = State.Idle;
			PlayAnim(AnimIdle, loop: true);
		}
		else if (animName == AnimMelee)
		{
			_state = State.Idle;
			PlayAnim(AnimIdle, loop: true);
		}
		else if (animName == AnimHurt)
		{
			// Resume the state we interrupted
			_state = _stateBeforeHurt;
			switch (_state)
			{
				case State.Idle:
					PlayAnim(AnimIdle, loop: true);
					break;
				case State.ThrowAttack:
					_throwEventFired = false;
					PlayAnim(AnimThrow);
					break;
				case State.MeleeAttack:
					_meleeEventFired = false;
					PlayAnim(AnimMelee);
					break;
				default:
					PlayAnim(AnimIdle, loop: true);
					break;
			}
		}
	}

	// -------------------------------------------------------------------------
	// Combat events
	// -------------------------------------------------------------------------

	private void SpawnProjectile()
	{
		// Projectile body — vacuumable so the player can pick it up and throw it back
		var projectile = new RigidBody3D();
		projectile.ContinuousCd = true;
		projectile.ContactMonitor = true;
		projectile.MaxContactsReported = 2;
		projectile.AddToGroup("vacuumable"); // vacuum can attract it
		projectile.AddToGroup("holdable");   // vacuum holds it instead of collecting it
		projectile.SetMeta("display_name", "Dr. Heffer Can");

		// Dr. Heffer model as the visual
		var modelScene = GD.Load<PackedScene>("res://Shared/Assets/Models/drhefferfixed2.glb");
		if (modelScene != null)
		{
			var model = modelScene.Instantiate<Node3D>();
			model.Scale = Vector3.One * 0.25f;
			projectile.AddChild(model);
		}
		else
		{
			// Fallback if the GLB isn't imported yet
			var fallback = new MeshInstance3D();
			fallback.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
			projectile.AddChild(fallback);
			GD.PrintErr("HoardAmalgamation: drhefferfixed2.glb not found — using fallback mesh.");
		}

		// Collision shape
		var col = new CollisionShape3D();
		col.Shape = new SphereShape3D { Radius = 0.35f };
		projectile.AddChild(col);

		// Spawn at the monster's hand position
		projectile.Position = GetThrowOrigin();
		GetTree().Root.AddChild(projectile);

		// Launch toward the player's chest height
		if (_player != null)
		{
			var target = _player.GlobalPosition + Vector3.Up * 1.0f;
			var dir = (target - projectile.Position).Normalized();
			projectile.LinearVelocity = dir * ProjectileSpeed;
			// Tumbling spin for character — rotate around a random axis
			projectile.AngularVelocity = new Vector3(
				_rng.RandfRange(-6f, 6f),
				_rng.RandfRange(-4f, 4f),
				_rng.RandfRange(-6f, 6f)
			);
		}

		// Deal damage once on first contact with the player.
		// The "can_damage_player" meta is set to false when the player grabs it,
		// so a reflected can never damages the player.
		projectile.SetMeta("can_damage_player", Variant.From(true));
		projectile.BodyEntered += (body) =>
		{
			if (!projectile.GetMeta("can_damage_player", Variant.From(true)).AsBool()) return;
			if (body is PlayerController pc)
			{
				projectile.SetMeta("can_damage_player", Variant.From(false)); // one hit only
				pc.TakeDamage(ProjectileDamage);
			}
		};

		// Auto-clean after 10 seconds so stray projectiles don't litter the room
		var lifetime = GetTree().CreateTimer(10.0);
		lifetime.Timeout += () =>
		{
			if (IsInstanceValid(projectile))
				projectile.QueueFree();
		};
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

	public void TakeDamage(float amount)
	{
		if (_state == State.Dead) return;

		_health = Mathf.Max(0.0f, _health - amount);

		while (_health <= _nextChunkThreshold && _nextChunkThreshold >= 0)
		{
			SpawnDebrisChunk();
			_nextChunkThreshold -= 5.0f; // one chunk per 5 HP → ~20 chunks over 100 HP
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

	/// <summary>
	/// Returns the world-space position to spawn the thrown projectile from.
	/// Tries to use the skeleton's actual hand bone for accuracy, falls back
	/// to a configurable local-space offset (ThrowOffset) on the monster body.
	/// </summary>
	private Vector3 GetThrowOrigin()
	{
		if (_skeleton != null)
		{
			// Try common humanoid right-hand bone names (Blender, Mixamo, etc.)
			string[] candidates =
			{
				"Hand.R", "hand.R", "RightHand", "right_hand", "Hand_R",
				"mixamorig:RightHand", "Bip01_R_Hand", "RHand", "r_hand",
				// Also try left hand variants in case the rig is mirrored
				"Hand.L", "hand.L", "LeftHand", "left_hand", "Hand_L",
			};
			foreach (var boneName in candidates)
			{
				int idx = _skeleton.FindBone(boneName);
				if (idx >= 0)
				{
					// GetBoneGlobalPose returns pose in skeleton-local space; multiply by skeleton's global transform
					var boneOrigin = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(idx).Origin;
					return boneOrigin;
				}
			}
		}

		// Fallback: offset in the monster's local space.
		// After FacePlayer(), -Z faces the player, so negative Z = forward.
		return GlobalPosition + GlobalTransform.Basis * ThrowOffset;
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
