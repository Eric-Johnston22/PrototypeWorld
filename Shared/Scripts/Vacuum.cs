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

	private Marker3D _nozzleTip;
	private Area3D _suctionArea;
	private MeshInstance3D _nozzleMesh;
	private GpuParticles3D _suctionParticles;
	private bool _isSucking;
	private bool _isBlowing;
	private int _collectedCount;
	private readonly List<RigidBody3D> _bodiesInRange = new();

	[Signal]
	public delegate void ItemCollectedEventHandler(int totalCollected, int maxCapacity, string objectName);

	[Signal]
	public delegate void VacuumStateChangedEventHandler(bool sucking, bool blowing);

	public int CollectedCount => _collectedCount;

	// Public API for other systems (e.g. HordeAmalgamation) to poll vacuum state
	public bool IsSucking => _isSucking;
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
		_isBlowing = Input.IsActionPressed("vacuum_blow") && !_isSucking;

		_suctionParticles.Emitting = _isSucking;

		// Bob the nozzle slightly when active
		if (_isSucking || _isBlowing)
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
		if (!_isSucking && !_isBlowing)
			return;

		var nozzlePos = _nozzleTip.GlobalPosition;
		var forward = -_nozzleTip.GlobalBasis.Z;

		// Process bodies in range - iterate backwards to safely remove
		for (int i = _bodiesInRange.Count - 1; i >= 0; i--)
		{
			var body = _bodiesInRange[i];
			if (!IsInstanceValid(body))
			{
				_bodiesInRange.RemoveAt(i);
				continue;
			}

			var toObject = body.GlobalPosition - nozzlePos;
			float distance = toObject.Length();
			var dirToObject = toObject.Normalized();

			// Check if within suction cone
			float angle = Mathf.RadToDeg(forward.AngleTo(dirToObject));
			if (angle > SuctionConeAngle)
				continue;

			if (_isSucking)
			{
				// Pull toward nozzle - force increases as object gets closer
				float forceMagnitude = SuctionForce / Mathf.Max(distance * 0.5f, 0.3f);

				// Get the object's mass resistance
				float massMultiplier = 1.0f / Mathf.Max(body.Mass, 0.1f);
				var force = -dirToObject * forceMagnitude * Mathf.Min(massMultiplier, 3.0f);

				// Add slight upward force to fight gravity when close
				if (distance < 5.0f)
					force.Y += 5.0f;

				// Add a slight spiral toward nozzle for visual flair
				var tangent = forward.Cross(dirToObject).Normalized();
				force += tangent * forceMagnitude * 0.15f;

				body.ApplyCentralForce(force);

				// Dampen velocity as object approaches for smoother collection
				if (distance < 3.0f)
					body.LinearVelocity = body.LinearVelocity.Lerp(Vector3.Zero, 0.05f);

				// Shrink object as it approaches nozzle
				if (distance < ShrinkStartDistance)
				{
					float shrinkFactor = Mathf.Clamp(distance / ShrinkStartDistance, 0.1f, 1.0f);
					body.Scale = Vector3.One * shrinkFactor;
				}

				// Collect if close enough
				if (distance < CollectDistance && _collectedCount < MaxCapacity)
				{
					CollectObject(body);
					_bodiesInRange.RemoveAt(i);
				}
			}
			else if (_isBlowing)
			{
				// Push away from nozzle
				float forceMagnitude = BlowForce / Mathf.Max(distance * 0.3f, 0.3f);
				body.ApplyCentralForce(dirToObject * forceMagnitude);

				// Add some upward kick for fun
				body.ApplyCentralForce(Vector3.Up * forceMagnitude * 0.3f);
			}
		}
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
