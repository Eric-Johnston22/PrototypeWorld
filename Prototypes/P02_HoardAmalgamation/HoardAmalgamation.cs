using Godot;

namespace Hoarders;

/// <summary>
/// The Hoard Amalgamation — a living pile of garbage that IS the room's enemy.
/// Uses an animation-driven state machine with four animations from the GLB:
///   idle        — default, plays when out of aggro range
///   throw       — ranged attack; can spawns at ThrowSpawnTime, launches at ThrowReleaseTime
///   hand_attack — melee attack when player is very close; debris burst at MeleeImpactTime
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
	[Export] public int MeleeDamage = 20;
	[Export] public int ProjectileDamage = 10;
	// Local-space offset from monster origin → projectile spawn point (right hand).
	// X = right, Y = height, Z = forward (-Z faces player after LookAt).
	// Overridden automatically if a hand bone is found on the skeleton.
	[Export] public Vector3 ThrowOffset = new(0.8f, 1.8f, -0.3f);

	// Animation names — must match exactly what Godot imported from the GLB.
	// Check the Output panel on first run; available names are printed there.
	[Export] public string AnimIdle   = "idle";
	[Export] public string AnimThrow  = "throw";
	[Export] public string AnimMelee  = "hand_attack";
	[Export] public string AnimHurt   = "hurt";

	// Tune these to match the exact keyframe moment in the GLB animations
	[Export] public float ThrowSpawnTime   = 1.0f;  // seconds into throw anim when can appears in hand
	[Export] public float ThrowReleaseTime = 3.5f;  // seconds into throw anim when can is launched
	[Export] public float MeleeLungeTime   = 1.5f;  // seconds into melee anim the monster charges forward
	[Export] public float MeleeImpactTime  = 4.02f; // seconds into melee anim when damage + shake land
	[Export] public float ThrowCooldown = 1.0f;     // seconds between throws
	[Export] public float MeleeCooldown = 5.75f;    // seconds between melee attacks
	[Export] public float HurtCooldown  = 0.5f;     // prevents hurt anim spamming every frame

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

	private bool _spawnEventFired = false; // visual proxy has appeared in hand
	private bool _throwEventFired = false; // can has been launched as RigidBody3D
	private bool _meleeEventFired = false;
	// Visual-only Node3D proxy held in hand during wind-up.
	// Using Node3D (not RigidBody3D) so GlobalPosition is never overridden by Jolt.
	private Node3D? _thrownProjectile = null;
	private float _throwCooldownTimer = 0f;
	private float _meleeCooldownTimer = 0f;
	private float _hurtCooldownTimer  = 0f;

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
		_meleeCooldownTimer = Mathf.Max(0f, _meleeCooldownTimer - dt);
		_hurtCooldownTimer  = Mathf.Max(0f, _hurtCooldownTimer  - dt);

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
			else if (dist <= MeleeDistance && _meleeCooldownTimer <= 0f)
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
				if (_animPlayer != null)
				{
					float animPos = (float)_animPlayer.CurrentAnimationPosition;

					// Phase 1 — visual proxy appears in the hand
					if (!_spawnEventFired && animPos >= ThrowSpawnTime)
					{
						_spawnEventFired = true;
						_thrownProjectile = CreateProjectileVisual();
					}

					// Pin the Node3D proxy to the hand every frame.
					// Node3D.GlobalPosition is never overridden by the physics engine.
					if (_thrownProjectile != null && animPos < ThrowReleaseTime)
						_thrownProjectile.GlobalPosition = GetThrowOrigin();

					// Phase 2 — swap visual proxy for a real RigidBody3D and launch it
					if (!_throwEventFired && _thrownProjectile != null && animPos >= ThrowReleaseTime)
					{
						_throwEventFired = true;
						LaunchProjectile(_thrownProjectile);
						_thrownProjectile = null;
					}
				}
				break;

			case State.MeleeAttack:
				// Lunge toward player during the initial charge window only
				if (_player != null
					&& _animPlayer != null
					&& _animPlayer.CurrentAnimationPosition < MeleeLungeTime)
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
				_spawnEventFired = false;
				_throwEventFired = false;
				DropHeldProjectile();
				PlayAnim(AnimThrow);
				break;

			case State.MeleeAttack:
				_meleeEventFired = false;
				_meleeCooldownTimer = MeleeCooldown;
				DropHeldProjectile();
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
					_spawnEventFired = false;
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

	/// <summary>
	/// Creates a plain Node3D with the Dr. Heffer visual so it can be pinned to
	/// the hand each frame without interference from the physics engine.
	/// Call LaunchProjectile() to replace it with a real RigidBody3D.
	/// </summary>
	private Node3D CreateProjectileVisual()
	{
		var visual = new Node3D();

		var modelScene = GD.Load<PackedScene>("res://Shared/Assets/Models/drhefferfixed2.glb");
		if (modelScene != null)
		{
			var model = modelScene.Instantiate<Node3D>();
			model.Scale = Vector3.One * 0.25f;
			visual.AddChild(model);
		}
		else
		{
			var fallback = new MeshInstance3D();
			fallback.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
			visual.AddChild(fallback);
			GD.PrintErr("HoardAmalgamation: drhefferfixed2.glb not found — using fallback mesh.");
		}

		GetTree().Root.AddChild(visual);
		visual.GlobalPosition = GetThrowOrigin();
		return visual;
	}

	/// <summary>
	/// Frees the visual proxy, creates a proper RigidBody3D at the same position,
	/// and launches it toward the player.
	/// </summary>
	private void LaunchProjectile(Node3D visual)
	{
		var launchPos = visual.GlobalPosition;
		visual.QueueFree();

		var projectile = new RigidBody3D();
		projectile.ContinuousCd = true;
		projectile.ContactMonitor = true;
		projectile.MaxContactsReported = 2;
		projectile.AddToGroup("vacuumable");
		projectile.AddToGroup("holdable");
		projectile.SetMeta("display_name", "Dr. Heffer Can");
		projectile.SetMeta("can_damage_player", Variant.From(true));

		var modelScene = GD.Load<PackedScene>("res://Shared/Assets/Models/drhefferfixed2.glb");
		if (modelScene != null)
		{
			var model = modelScene.Instantiate<Node3D>();
			model.Scale = Vector3.One * 0.25f;
			projectile.AddChild(model);
		}
		else
		{
			var fallback = new MeshInstance3D();
			fallback.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
			projectile.AddChild(fallback);
		}

		var col = new CollisionShape3D();
		col.Shape = new SphereShape3D { Radius = 0.35f };
		projectile.AddChild(col);

		GetTree().Root.AddChild(projectile);
		projectile.GlobalPosition = launchPos;
		projectile.AddCollisionExceptionWith(this);

		// Throw toward player's chest
		if (_player != null)
		{
			var target = _player.GlobalPosition + Vector3.Up * 1.0f;
			var dir = (target - projectile.GlobalPosition).Normalized();
			projectile.LinearVelocity = dir * ProjectileSpeed;
			projectile.AngularVelocity = new Vector3(
				_rng.RandfRange(-6f, 6f),
				_rng.RandfRange(-4f, 4f),
				_rng.RandfRange(-6f, 6f)
			);
		}

		// Damage player on first contact
		projectile.BodyEntered += (body) =>
		{
			if (!projectile.GetMeta("can_damage_player", Variant.From(true)).AsBool()) return;
			if (body is PlayerController pc)
			{
				projectile.SetMeta("can_damage_player", Variant.From(false));
				pc.TakeDamage(ProjectileDamage);
			}
		};

		// Auto-clean after 10 seconds
		var lifetime = GetTree().CreateTimer(10.0);
		lifetime.Timeout += () =>
		{
			if (IsInstanceValid(projectile))
				projectile.QueueFree();
		};
	}

	/// <summary>
	/// Discards the held visual proxy (if any).
	/// Called when the throw is interrupted by melee, hurt, or death.
	/// </summary>
	private void DropHeldProjectile()
	{
		if (_thrownProjectile == null || !IsInstanceValid(_thrownProjectile)) return;
		_thrownProjectile.QueueFree();
		_thrownProjectile = null;
	}

	private void OnMeleeImpact()
	{
		// Visual feedback — debris burst at impact
		for (int i = 0; i < 4; i++)
			SpawnDebrisChunk();

		// Damage + screen shake land at the exact same moment
		if (_player is PlayerController pc)
		{
			pc.TakeDamage(MeleeDamage);
			pc.AddTrauma(0.85f);
		}
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
			DropHeldProjectile(); // discard visual proxy before interrupting the throw
			_state = State.Hurt;
			_hurtCooldownTimer = HurtCooldown;
			PlayAnim(AnimHurt);
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
			string[] candidates =
			{
				"Hand.R", "hand.R", "RightHand", "right_hand", "Hand_R",
				"mixamorig:RightHand", "Bip01_R_Hand", "RHand", "r_hand",
				"Hand.L", "hand.L", "LeftHand", "left_hand", "Hand_L",
			};
			foreach (var boneName in candidates)
			{
				int idx = _skeleton.FindBone(boneName);
				if (idx >= 0)
				{
					var boneOrigin = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(idx).Origin;
					return boneOrigin;
				}
			}
		}

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
