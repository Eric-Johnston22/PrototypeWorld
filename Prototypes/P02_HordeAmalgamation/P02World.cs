using Godot;

namespace Hoarders;

/// <summary>
/// P02 environment: a grimy hoarder room — dirty pink walls, dim lighting,
/// scattered static debris piles. The stage for fighting the Horde Amalgamation.
/// </summary>
public partial class P02World : Node3D
{
    [Export] public float RoomWidth = 14.0f;
    [Export] public float RoomDepth = 12.0f;
    [Export] public float RoomHeight = 4.5f;
    [Export] public float WallThickness = 0.3f;

    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        BuildRoom();
        SpawnAtmosphericDebris();
    }

    private void BuildRoom()
    {
        float hw = RoomWidth / 2.0f;
        float hd = RoomDepth / 2.0f;

        // Floor — dark grimy brown
        CreateStaticBox("Floor",
            new Vector3(0, -0.15f, 0),
            new Vector3(RoomWidth, WallThickness, RoomDepth),
            new Color(0.28f, 0.22f, 0.18f));

        // Ceiling — dingy off-white
        CreateStaticBox("Ceiling",
            new Vector3(0, RoomHeight + 0.15f, 0),
            new Vector3(RoomWidth, WallThickness, RoomDepth),
            new Color(0.72f, 0.68f, 0.65f));

        // Walls — dirty pink (hoarder house aesthetic)
        CreateStaticBox("WallNorth",
            new Vector3(0, RoomHeight / 2, -hd),
            new Vector3(RoomWidth, RoomHeight, WallThickness),
            new Color(0.72f, 0.52f, 0.52f));
        CreateStaticBox("WallSouth",
            new Vector3(0, RoomHeight / 2, hd),
            new Vector3(RoomWidth, RoomHeight, WallThickness),
            new Color(0.7f, 0.5f, 0.5f));
        CreateStaticBox("WallEast",
            new Vector3(hw, RoomHeight / 2, 0),
            new Vector3(WallThickness, RoomHeight, RoomDepth),
            new Color(0.68f, 0.48f, 0.48f));
        CreateStaticBox("WallWest",
            new Vector3(-hw, RoomHeight / 2, 0),
            new Vector3(WallThickness, RoomHeight, RoomDepth),
            new Color(0.68f, 0.48f, 0.48f));

        SetupLighting();
    }

    private void SetupLighting()
    {
        var env = new WorldEnvironment();
        var envRes = new Godot.Environment();
        envRes.BackgroundMode = Godot.Environment.BGMode.Color;
        envRes.BackgroundColor = new Color(0.08f, 0.05f, 0.05f);
        envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        envRes.AmbientLightColor = new Color(0.22f, 0.15f, 0.15f);
        envRes.AmbientLightEnergy = 0.5f;
        envRes.SsaoEnabled = true;
        env.Environment = envRes;
        AddChild(env);

        // Flickery main overhead light
        var mainLight = new OmniLight3D();
        mainLight.Position = new Vector3(0, RoomHeight - 0.4f, 0);
        mainLight.OmniRange = 16.0f;
        mainLight.LightEnergy = 1.5f;
        mainLight.LightColor = new Color(1.0f, 0.88f, 0.72f);
        mainLight.ShadowEnabled = true;
        mainLight.Name = "MainLight";
        AddChild(mainLight);

        // Ominous red glow from monster side of room
        var monsterGlow = new OmniLight3D();
        monsterGlow.Position = new Vector3(0, 1.0f, -4.0f);
        monsterGlow.OmniRange = 7.0f;
        monsterGlow.LightEnergy = 0.7f;
        monsterGlow.LightColor = new Color(1.0f, 0.2f, 0.1f);
        monsterGlow.Name = "MonsterGlow";
        AddChild(monsterGlow);
    }

    private void SpawnAtmosphericDebris()
    {
        // Static (non-vacuumable) junk piles for atmosphere along the walls
        float hw = RoomWidth / 2.0f - 0.5f;
        float hd = RoomDepth / 2.0f - 0.5f;

        // Piles along each wall
        SpawnDebrisPile(new Vector3(-hw + 0.5f, 0, _rng.RandfRange(-hd + 1, hd - 1)));
        SpawnDebrisPile(new Vector3(hw - 0.5f, 0, _rng.RandfRange(-hd + 1, hd - 1)));
        SpawnDebrisPile(new Vector3(_rng.RandfRange(-hw + 2, hw - 2), 0, -hd + 0.5f));
        SpawnDebrisPile(new Vector3(_rng.RandfRange(-3f, -1f), 0, hd - 0.8f));
        SpawnDebrisPile(new Vector3(_rng.RandfRange(1f, 3f), 0, hd - 0.8f));
    }

    private void SpawnDebrisPile(Vector3 basePosition)
    {
        int count = _rng.RandiRange(4, 8);
        for (int i = 0; i < count; i++)
        {
            float s = _rng.RandfRange(0.2f, 0.55f);
            var offset = new Vector3(
                _rng.RandfRange(-0.6f, 0.6f),
                s * 0.5f + i * 0.1f,
                _rng.RandfRange(-0.6f, 0.6f));

            var body = new StaticBody3D();
            body.Position = basePosition + offset;
            body.Rotation = new Vector3(
                _rng.RandfRange(-0.3f, 0.3f),
                _rng.RandfRange(0, Mathf.Tau),
                _rng.RandfRange(-0.3f, 0.3f));

            var mesh = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(s * 1.4f, s * 0.7f, s);
            mesh.Mesh = box;

            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(
                _rng.RandfRange(0.3f, 0.6f),
                _rng.RandfRange(0.25f, 0.45f),
                _rng.RandfRange(0.15f, 0.35f));
            mat.Roughness = 0.95f;
            mesh.MaterialOverride = mat;
            body.AddChild(mesh);

            var col = new CollisionShape3D();
            var shape = new BoxShape3D();
            shape.Size = box.Size;
            col.Shape = shape;
            body.AddChild(col);

            AddChild(body);
        }
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
        mat.Roughness = 0.9f;
        mesh.MaterialOverride = mat;
        body.AddChild(mesh);

        var col = new CollisionShape3D();
        var shape = new BoxShape3D();
        shape.Size = size;
        col.Shape = shape;
        body.AddChild(col);

        AddChild(body);
    }
}
