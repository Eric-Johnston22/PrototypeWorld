using Godot;

namespace Hoarders;

/// <summary>
/// Builds the P01 prototype level: a messy room with furniture and scattered suckable objects.
/// Attach to the Main scene root Node3D.
/// </summary>
public partial class GameWorld : Node3D
{
	[Export] public PackedScene PlayerScene;
	[Export] public float RoomWidth = 24.0f;
	[Export] public float RoomDepth = 20.0f;
	[Export] public float RoomHeight = 5.0f;
	[Export] public float WallThickness = 0.3f;

	private Node3D _objectsContainer;

	private readonly RandomNumberGenerator _rng = new();

	// Whimsical object definitions - TF2/Luigi's Mansion inspired
	private static readonly (string name, Color color, VacuumableObject.ObjectSize size, float scale)[] ObjectTypes =
	{
		("Dust Bunny",    new Color(0.7f, 0.65f, 0.6f),  VacuumableObject.ObjectSize.Small,  0.15f),
		("Lost Sock",     new Color(0.3f, 0.5f, 0.8f),   VacuumableObject.ObjectSize.Small,  0.18f),
		("Sandwich",      new Color(0.85f, 0.7f, 0.3f),  VacuumableObject.ObjectSize.Small,  0.2f),
		("Coffee Mug",    new Color(0.9f, 0.9f, 0.85f),  VacuumableObject.ObjectSize.Medium, 0.22f),
		("Rubber Duck",   new Color(1.0f, 0.85f, 0.0f),  VacuumableObject.ObjectSize.Small,  0.2f),
		("Hat",           new Color(0.6f, 0.2f, 0.2f),   VacuumableObject.ObjectSize.Medium, 0.3f),
		("Bowling Ball",  new Color(0.1f, 0.1f, 0.1f),   VacuumableObject.ObjectSize.Medium, 0.25f),
		("Ghost Orb",     new Color(0.4f, 0.9f, 0.5f),   VacuumableObject.ObjectSize.Small,  0.2f),
		("Boot",          new Color(0.4f, 0.25f, 0.1f),  VacuumableObject.ObjectSize.Medium, 0.25f),
		("Teddy Bear",    new Color(0.65f, 0.4f, 0.2f),  VacuumableObject.ObjectSize.Medium, 0.3f),
		("Pizza Box",     new Color(0.8f, 0.6f, 0.3f),   VacuumableObject.ObjectSize.Medium, 0.4f),
		("Pumpkin",       new Color(0.9f, 0.5f, 0.1f),   VacuumableObject.ObjectSize.Large,  0.35f),
		("Crate",         new Color(0.6f, 0.45f, 0.25f), VacuumableObject.ObjectSize.Large,  0.5f),
		("Barrel",        new Color(0.5f, 0.35f, 0.2f),  VacuumableObject.ObjectSize.Large,  0.45f),
	};

	public override void _Ready()
	{
		_rng.Randomize();
		_objectsContainer = new Node3D { Name = "VacuumableObjects" };
		AddChild(_objectsContainer);

		BuildRoom();
		BuildFurniture();
		SpawnVacuumableObjects();
	}

	private void BuildRoom()
	{
		float hw = RoomWidth / 2.0f;
		float hd = RoomDepth / 2.0f;

		// Floor
		CreateStaticBox("Floor",
			new Vector3(0, -0.15f, 0),
			new Vector3(RoomWidth, WallThickness, RoomDepth),
			new Color(0.35f, 0.3f, 0.25f));

		// Ceiling
		CreateStaticBox("Ceiling",
			new Vector3(0, RoomHeight + 0.15f, 0),
			new Vector3(RoomWidth, WallThickness, RoomDepth),
			new Color(0.8f, 0.78f, 0.75f));

		// Walls
		CreateStaticBox("WallNorth",
			new Vector3(0, RoomHeight / 2, -hd),
			new Vector3(RoomWidth, RoomHeight, WallThickness),
			new Color(0.55f, 0.6f, 0.5f));

		CreateStaticBox("WallSouth",
			new Vector3(0, RoomHeight / 2, hd),
			new Vector3(RoomWidth, RoomHeight, WallThickness),
			new Color(0.55f, 0.6f, 0.5f));

		CreateStaticBox("WallEast",
			new Vector3(hw, RoomHeight / 2, 0),
			new Vector3(WallThickness, RoomHeight, RoomDepth),
			new Color(0.5f, 0.55f, 0.6f));

		CreateStaticBox("WallWest",
			new Vector3(-hw, RoomHeight / 2, 0),
			new Vector3(WallThickness, RoomHeight, RoomDepth),
			new Color(0.5f, 0.55f, 0.6f));

		// Environment lighting
		var env = new WorldEnvironment();
		var envRes = new Godot.Environment();
		envRes.BackgroundMode = Godot.Environment.BGMode.Color;
		envRes.BackgroundColor = new Color(0.15f, 0.12f, 0.18f);
		envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		envRes.AmbientLightColor = new Color(0.3f, 0.28f, 0.35f);
		envRes.AmbientLightEnergy = 0.6f;
		envRes.SsaoEnabled = true;
		env.Environment = envRes;
		AddChild(env);

		// Main overhead light
		var mainLight = new OmniLight3D();
		mainLight.Position = new Vector3(0, RoomHeight - 0.5f, 0);
		mainLight.OmniRange = 18.0f;
		mainLight.LightEnergy = 1.8f;
		mainLight.LightColor = new Color(1.0f, 0.92f, 0.8f);
		mainLight.ShadowEnabled = true;
		mainLight.Name = "MainLight";
		AddChild(mainLight);

		// Secondary accent light (Luigi's Mansion vibe)
		var accentLight = new OmniLight3D();
		accentLight.Position = new Vector3(-8, 3, -6);
		accentLight.OmniRange = 10.0f;
		accentLight.LightEnergy = 0.7f;
		accentLight.LightColor = new Color(0.5f, 0.5f, 0.9f);
		accentLight.Name = "AccentLight";
		AddChild(accentLight);

		// Warm corner light
		var warmLight = new OmniLight3D();
		warmLight.Position = new Vector3(8, 2.5f, 6);
		warmLight.OmniRange = 8.0f;
		warmLight.LightEnergy = 0.5f;
		warmLight.LightColor = new Color(1.0f, 0.7f, 0.4f);
		warmLight.Name = "WarmLight";
		AddChild(warmLight);
	}

	private void BuildFurniture()
	{
		// Large table in the center
		CreateStaticBox("Table",
			new Vector3(0, 0.45f, 0),
			new Vector3(3.0f, 0.1f, 1.5f),
			new Color(0.45f, 0.3f, 0.15f));
		CreateStaticBox("TableLeg1", new Vector3(-1.3f, 0.2f, -0.6f), new Vector3(0.1f, 0.4f, 0.1f), new Color(0.4f, 0.25f, 0.12f));
		CreateStaticBox("TableLeg2", new Vector3(1.3f, 0.2f, -0.6f), new Vector3(0.1f, 0.4f, 0.1f), new Color(0.4f, 0.25f, 0.12f));
		CreateStaticBox("TableLeg3", new Vector3(-1.3f, 0.2f, 0.6f), new Vector3(0.1f, 0.4f, 0.1f), new Color(0.4f, 0.25f, 0.12f));
		CreateStaticBox("TableLeg4", new Vector3(1.3f, 0.2f, 0.6f), new Vector3(0.1f, 0.4f, 0.1f), new Color(0.4f, 0.25f, 0.12f));

		// Bookshelf against north wall
		CreateStaticBox("Bookshelf",
			new Vector3(-6, 1.5f, -9.3f),
			new Vector3(3.0f, 3.0f, 0.6f),
			new Color(0.5f, 0.35f, 0.18f));
		CreateStaticBox("Shelf1", new Vector3(-6, 1.0f, -9.3f), new Vector3(2.8f, 0.05f, 0.5f), new Color(0.45f, 0.3f, 0.15f));
		CreateStaticBox("Shelf2", new Vector3(-6, 2.0f, -9.3f), new Vector3(2.8f, 0.05f, 0.5f), new Color(0.45f, 0.3f, 0.15f));

		// Couch along east wall
		CreateStaticBox("CouchSeat",
			new Vector3(9.5f, 0.35f, 0),
			new Vector3(1.2f, 0.35f, 3.5f),
			new Color(0.3f, 0.25f, 0.5f));
		CreateStaticBox("CouchBack",
			new Vector3(10.3f, 0.8f, 0),
			new Vector3(0.25f, 0.7f, 3.5f),
			new Color(0.28f, 0.22f, 0.48f));

		// Desk in corner
		CreateStaticBox("Desk",
			new Vector3(7, 0.55f, -7),
			new Vector3(2.0f, 0.08f, 1.0f),
			new Color(0.6f, 0.5f, 0.35f));
		CreateStaticBox("DeskLeg1", new Vector3(6.1f, 0.25f, -6.6f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.55f, 0.45f, 0.3f));
		CreateStaticBox("DeskLeg2", new Vector3(7.9f, 0.25f, -6.6f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.55f, 0.45f, 0.3f));
		CreateStaticBox("DeskLeg3", new Vector3(6.1f, 0.25f, -7.4f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.55f, 0.45f, 0.3f));
		CreateStaticBox("DeskLeg4", new Vector3(7.9f, 0.25f, -7.4f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.55f, 0.45f, 0.3f));

		// Side table
		CreateStaticBox("SideTable",
			new Vector3(-9, 0.4f, 5),
			new Vector3(1.0f, 0.06f, 1.0f),
			new Color(0.5f, 0.4f, 0.3f));
	}

	private void SpawnVacuumableObjects()
	{
		float hw = RoomWidth / 2.0f - 1.5f;
		float hd = RoomDepth / 2.0f - 1.5f;

		int totalObjects = 200;
		for (int i = 0; i < totalObjects; i++)
		{
			var def = ObjectTypes[_rng.RandiRange(0, ObjectTypes.Length - 1)];

			float x = _rng.RandfRange(-hw, hw);
			float z = _rng.RandfRange(-hd, hd);
			float y = def.size == VacuumableObject.ObjectSize.Small ? 0.3f : 0.5f;

			// Some objects start on the table
			if (i < 8)
			{
				x = _rng.RandfRange(-1.3f, 1.3f);
				z = _rng.RandfRange(-0.6f, 0.6f);
				y = 0.8f;
			}
			// Some on the desk
			else if (i < 12)
			{
				x = _rng.RandfRange(6.2f, 7.8f);
				z = _rng.RandfRange(-7.3f, -6.7f);
				y = 0.9f;
			}
			// Some on the bookshelf
			else if (i < 15)
			{
				x = _rng.RandfRange(-7.3f, -4.7f);
				z = _rng.RandfRange(-9.5f, -9.1f);
				y = _rng.RandfRange(1.1f, 2.5f);
			}

			SpawnObject(def.name, def.color, def.size, def.scale, new Vector3(x, y, z));
		}

		// Ghost orbs — floating, Luigi's Mansion tribute
		for (int i = 0; i < 5; i++)
		{
			float x = _rng.RandfRange(-hw, hw);
			float z = _rng.RandfRange(-hd, hd);
			float y = _rng.RandfRange(2.0f, 4.0f);

			var ghost = SpawnObject("Ghost Orb", new Color(0.3f, 1.0f, 0.5f, 0.8f),
				VacuumableObject.ObjectSize.Small, 0.25f, new Vector3(x, y, z));

			ghost.GravityScale = 0.05f;
			ghost.LinearDamp = 2.0f;

			var omni = new OmniLight3D();
			omni.LightColor = new Color(0.3f, 1.0f, 0.5f);
			omni.LightEnergy = 0.5f;
			omni.OmniRange = 2.5f;
			ghost.AddChild(omni);
		}
	}

	private VacuumableObject SpawnObject(string displayName, Color color,
		VacuumableObject.ObjectSize size, float scale, Vector3 position)
	{
		var body = new VacuumableObject();
		body.Name = displayName.Replace(" ", "");
		body.DisplayName = displayName;
		body.ObjectColor = color;
		body.Size = size;
		body.Position = position;

		body.Rotation = new Vector3(
			_rng.RandfRange(0, Mathf.Tau),
			_rng.RandfRange(0, Mathf.Tau),
			_rng.RandfRange(0, Mathf.Tau));

		Mesh mesh;
		if (displayName.Contains("Ball") || displayName.Contains("Orb") ||
			displayName.Contains("Pumpkin") || displayName.Contains("Duck"))
		{
			var sphere = new SphereMesh();
			sphere.Radius = scale;
			sphere.Height = scale * 2.0f;
			mesh = sphere;
		}
		else if (displayName.Contains("Barrel") || displayName.Contains("Mug") ||
				 displayName.Contains("Boot"))
		{
			var cylinder = new CylinderMesh();
			cylinder.TopRadius = scale * 0.7f;
			cylinder.BottomRadius = scale;
			cylinder.Height = scale * 2.0f;
			mesh = cylinder;
		}
		else if (displayName.Contains("Hat"))
		{
			var cone = new CylinderMesh();
			cone.TopRadius = 0.02f;
			cone.BottomRadius = scale;
			cone.Height = scale * 2.0f;
			mesh = cone;
		}
		else
		{
			var box = new BoxMesh();
			box.Size = new Vector3(scale * 1.2f, scale, scale * 0.8f);
			mesh = box;
		}

		var meshInstance = new MeshInstance3D();
		meshInstance.Mesh = mesh;
		body.AddChild(meshInstance);

		var collision = new CollisionShape3D();
		if (mesh is SphereMesh sm)
		{
			var shape = new SphereShape3D();
			shape.Radius = sm.Radius;
			collision.Shape = shape;
		}
		else if (mesh is CylinderMesh cm)
		{
			var shape = new CylinderShape3D();
			shape.Radius = cm.BottomRadius;
			shape.Height = cm.Height;
			collision.Shape = shape;
		}
		else if (mesh is BoxMesh bm)
		{
			var shape = new BoxShape3D();
			shape.Size = bm.Size;
			collision.Shape = shape;
		}
		body.AddChild(collision);

		body.ContinuousCd = true;
		body.ContactMonitor = true;
		body.MaxContactsReported = 2;

		_objectsContainer.AddChild(body);
		return body;
	}

	private void CreateStaticBox(string name, Vector3 position, Vector3 size, Color color)
	{
		var body = new StaticBody3D();
		body.Name = name;
		body.Position = position;

		var mesh = new MeshInstance3D();
		var boxMesh = new BoxMesh();
		boxMesh.Size = size;
		mesh.Mesh = boxMesh;
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = color;
		mat.Roughness = 0.85f;
		mesh.MaterialOverride = mat;
		body.AddChild(mesh);

		var collision = new CollisionShape3D();
		var shape = new BoxShape3D();
		shape.Size = size;
		collision.Shape = shape;
		body.AddChild(collision);

		AddChild(body);
	}
}
