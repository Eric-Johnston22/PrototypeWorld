using Godot;
using System.Collections.Generic;

namespace Hoarders;

/// <summary>
/// Vacuum tool that sucks up or blows away physics objects.
/// Attach to a Node3D that is a child of the player's Head/Camera.
/// Expects a "NozzleTip" Marker3D child and "SuctionArea" Area3D child.
/// </summary>
public partial class Vacuum : Node3D
{
	[Export] public float SuctionForce = 20.0f;
	[Export] public float BlowForce = 25.0f;
	[Export] public float SuctionConeAngle = 35.0f;
	[Export] public float CollectDistance = 0.8f;
	[Export] public int MaxCapacity = 100;
	[Export] public float ShrinkStartDistance = 3.0f;
	[Export] public float FireSpeed = 28.0f;       // speed of a reflected holdable projectile
	[Export] public int ReflectDamage = 25;        // damage dealt to monster when can is fired back

	private Marker3D _nozzleTip;
	private Area3D _suctionArea;
	private MeshInstance3D _nozzleMesh;
	private GpuParticles3D _suctionParticles;
	private bool _isSucking;
	private bool _isBlowing;
	private int _collectedCount;
	private RigidBody3D? _heldObject = null;       // currently held "holdable" projectile
	private readonly List<RigidBody3D> _bodiesInRange = new();

	[Signal]
	public delegate void ItemCollectedEventHandler(int totalCollected, int maxCapacity, string objectName);

	[Signal]
	public delegate void VacuumStateChangedEventHandler(bool sucking, bool blowing);

	public int CollectedCount => _collectedCount;

	// Public API for other systems (e.g. HordeAmalgamation) to poll vacuum state
	public bool IsSucking => _isSucking && _heldObject == null;
	public bool IsBlowing => _isBlowing;
	public Vector3 NozzleGlobalPosition => _nozzleTip != null ? _nozzleTip.GlobalPosition : GlobalPosition;
	public Vector3 NozzleForward => _nozzleTip != null ? -_nozzleTip.GlobalBasis.Z : -GlobalBasis.Z;

	public override void _Ready()
	{
		AddToGroup("vacuum");
		_nozzleTip = GetNode<Marker3D>("NozzleTip");
		_suctionArea = GetNode<Area3D>("SuctionArea");
		_nozzleMesh = GetNode<MeshInstance3D>("NozzleMesh");
		_suctionParticles = GetNode<GpuParticles3D>("SuctionParticles");

		_suctionArea.BodyEntered += OnBodyEntered;
		_suctionArea.BodyExited += OnBodyExited;

		_suctionParticles.Emitting = false;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is RigidBody3D rb && body.IsInGroup("vacuumable"))
			_bodiesInRange.Add(rb);
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is RigidBody3D rb)
			_bodiesInRange.Remove(rb);
	}

	public override void _Process(double delta)
	{
		bool wasSucking = _isSucking;
		bool wasBlowing = _isBlowing;

		_isSucking = Input.IsActionPressed("vacuum_suck") && _collectedCount < MaxCapacity;

		// Fire the held object with the blow action (takes priority over normal blow)
		if (_heldObject != null && Input.IsActionJustPressed("vacuum_blow"))
		{
			FireHeldObject();
			_isBlowing = false;
		}
		else
		{
			// Normal blow only works when not sucking and not holding anything
			_isBlowing = Input.IsActionPressed("vacuum_blow") && !_isSucking && _heldObject == null;
		}

		_suctionParticles.Emitting = _isSucking && _heldObject == null;

		// Bob the nozzle slightly when active or holding
		if (_isSucking || _isBlowing || _heldObject != null)
		{
			float bob = Mathf.Sin((float)Time.GetTicksMsec() / 80.0f) * 0.002f;
			var pos = _nozzleMesh.Position;
			pos.Y = -0.15f + bob;
			_nozzleMesh.Position = pos;
		}

		if (wasSucking != _isSucking || wasBlowing != _isBlowing)
			EmitSignal(SignalName.VacuumStateChanged, _isSucking, _isBlowing);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_isSucking && !_isBlowing && _heldObject == null)
			return;

		var nozzlePos = _nozzleTip.GlobalPosition;
		var forward = -_nozzleTip.GlobalBasis.Z;

		// ── Held object: spring to nozzle or drop ────────────────────────────
		if (_heldObject != null)
		{
			if (!_isSucking)
			{
				DropHeldObject();
			}
			else
			{
				// Spring the can to a point just in front of the nozzle tip
				var holdPoint = nozzlePos + forward * 0.6f;
				_heldObject.LinearVelocity = (holdPoint - _heldObject.GlobalPosition) * 25f;
				_heldObject.AngularVelocity = new Vector3(0f, 2f, 0f); // gentle spin while held
			}
			return; // skip normal suction / blow this frame
		}

		// ── Normal suction / blow ─────────────────────────────────────────────

		// If a holdable projectile is incoming, focus all suction on it — nothing else gets pulled in
		bool holdableLock = false;
		if (_isSucking)
		{
			foreach (var b in _bodiesInRange)
			{
				if (IsInstanceValid(b) && b.IsInGroup("holdable"))
				{
					holdableLock = true;
					break;
				}
			}
		}

		for (int i = _bodiesInRange.Count - 1; i >= 0; i--)
		{
			var body = _bodiesInRange[i];
			if (!IsInstanceValid(body))
			{
				_bodiesInRange.RemoveAt(i);
				continue;
			}

			// Focused suction: skip non-holdable objects while a can is incoming
			if (holdableLock && !body.IsInGroup("holdable"))
				continue;

			var toObject = body.GlobalPosition - nozzlePos;
			float distance = toObject.Length();
			var dirToObject = toObject.Normalized();

			// Check if within suction cone
			float angle = Mathf.RadToDeg(forward.AngleTo(dirToObject));
			if (angle > SuctionConeAngle)
				continue;

			if (_isSucking)
			{
				// Pull toward nozzle — force increases as object gets closer
				float forceMagnitude = SuctionForce / Mathf.Max(distance * 0.5f, 0.3f);
				float massMultiplier = 1.0f / Mathf.Max(body.Mass, 0.1f);
				var force = -dirToObject * forceMagnitude * Mathf.Min(massMultiplier, 3.0f);
				if (distance < 5.0f)
					force.Y += 5.0f;
				var tangent = forward.Cross(dirToObject).Normalized();
				force += tangent * forceMagnitude * 0.15f;
				body.ApplyCentralForce(force);

				if (distance < 3.0f)
					body.LinearVelocity = body.LinearVelocity.Lerp(Vector3.Zero, 0.05f);

				if (distance < ShrinkStartDistance)
				{
					float shrinkFactor = Mathf.Clamp(distance / ShrinkStartDistance, 0.1f, 1.0f);
					body.Scale = Vector3.One * shrinkFactor;
				}

				if (distance < CollectDistance)
				{
					// "holdable" objects are grabbed and held, not consumed
					if (body.IsInGroup("holdable"))
					{
						GrabObject(body);
						_bodiesInRange.RemoveAt(i);
					}
					else if (_collectedCount < MaxCapacity)
					{
						CollectObject(body);
						_bodiesInRange.RemoveAt(i);
					}
				}
			}
			else if (_isBlowing)
			{
				float forceMagnitude = BlowForce / Mathf.Max(distance * 0.3f, 0.3f);
				body.ApplyCentralForce(dirToObject * forceMagnitude);
				body.ApplyCentralForce(Vector3.Up * forceMagnitude * 0.3f);
			}
		}
	}

	// ── Hold / fire methods ───────────────────────────────────────────────────

	private void GrabObject(RigidBody3D body)
	{
		_heldObject = body;
		_bodiesInRange.Remove(body);

		// Remove from "vacuumable" so the SuctionArea won't re-detect it while held
		body.RemoveFromGroup("vacuumable");

		// Suspend physics: zero gravity, no collision (can't hit walls or player while held)
		body.GravityScale = 0f;
		body.CollisionLayer = 0;
		body.CollisionMask = 0;
		body.LinearVelocity = Vector3.Zero;
		body.AngularVelocity = Vector3.Zero;
		body.Scale = Vector3.One; // restore from suction shrink

		// Prevent the original player-damage callback from firing after it's been grabbed
		if (body.HasMeta("can_damage_player"))
			body.SetMeta("can_damage_player", Variant.From(false));
	}

	private void DropHeldObject()
	{
		if (_heldObject == null) return;
		var obj = _heldObject;
		_heldObject = null;

		obj.GravityScale = 1f;
		obj.CollisionLayer = 1;
		obj.CollisionMask = 1;
		obj.LinearVelocity = Vector3.Zero;
		obj.AngularVelocity = Vector3.Zero;

		// Re-enable suction after a short delay so it can't be immediately re-grabbed
		var timer = GetTree().CreateTimer(0.5);
		timer.Timeout += () => { if (IsInstanceValid(obj)) obj.AddToGroup("vacuumable"); };
	}

	private void FireHeldObject()
	{
		if (_heldObject == null) return;
		var obj = _heldObject;
		_heldObject = null;

		obj.GravityScale = 1f;
		obj.CollisionLayer = 1;
		obj.CollisionMask = 1;
		obj.Scale = Vector3.One;

		// Launch in the direction the player is aiming
		var fireDir = -_nozzleTip.GlobalBasis.Z;
		obj.LinearVelocity = fireDir * FireSpeed;
		obj.AngularVelocity = new Vector3(
			(GD.Randf() - 0.5f) * 14f,
			(GD.Randf() - 0.5f) * 10f,
			(GD.Randf() - 0.5f) * 14f
		);

		// Brief window before contact monitoring re-enables:
		// prevents the can from immediately re-triggering on the player or nozzle
		var timer = GetTree().CreateTimer(0.15);
		timer.Timeout += () =>
		{
			if (!IsInstanceValid(obj)) return;

			// Re-enable suction (player could catch it again if they miss)
			obj.AddToGroup("vacuumable");

			// Wire up monster damage on first hit
			obj.ContactMonitor = true;
			obj.MaxContactsReported = 2;
			obj.BodyEntered += (body) =>
			{
				if (body is HoardAmalgamation monster)
					monster.TakeDamage(ReflectDamage);
			};
		};
	}

	private void CollectObject(RigidBody3D body)
	{
		_collectedCount++;
		string objName = body.GetMeta("display_name", "Object").AsString();

		// Spawn a brief collection effect at the object's position
		SpawnCollectEffect(body.GlobalPosition);

		body.QueueFree();

		EmitSignal(SignalName.ItemCollected, _collectedCount, MaxCapacity, objName);
	}

	private void SpawnCollectEffect(Vector3 position)
	{
		var particles = new GpuParticles3D();
		var material = new ParticleProcessMaterial();
		material.Direction = new Vector3(0, 1, 0);
		material.Spread = 180.0f;
		material.InitialVelocityMin = 2.0f;
		material.InitialVelocityMax = 5.0f;
		material.Gravity = Vector3.Zero;
		material.ScaleMin = 0.05f;
		material.ScaleMax = 0.15f;
		material.Color = new Color(0.5f, 0.8f, 1.0f, 0.8f);

		var mesh = new SphereMesh();
		mesh.Radius = 0.05f;
		mesh.Height = 0.1f;

		particles.ProcessMaterial = material;
		particles.DrawPass1 = mesh;
		particles.Amount = 12;
		particles.OneShot = true;
		particles.Explosiveness = 0.9f;
		particles.Lifetime = 0.4f;

		GetTree().Root.AddChild(particles);
		particles.GlobalPosition = position;
		particles.Emitting = true;

		// Auto-cleanup
		var timer = GetTree().CreateTimer(1.0);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(particles))
				particles.QueueFree();
		};
	}
}
