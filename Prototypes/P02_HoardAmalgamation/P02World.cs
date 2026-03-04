using Godot;

namespace Hoarders;

/// <summary>
/// P02 environment: a grand Victorian living room ca. 1920 — dark wood wainscoting,
/// deep green wallpaper, Persian rug, chesterfield sofa, fireplace, chandelier.
/// The stage for fighting the Hoard Amalgamation.
/// </summary>
public partial class P02World : Node3D
{
	[Export] public float RoomWidth = 30.0f;
	[Export] public float RoomDepth = 26.0f;
	[Export] public float RoomHeight = 6.5f;
	[Export] public float WallThickness = 0.4f;

	private readonly RandomNumberGenerator _rng = new();

	// Victorian palette
	private static readonly Color FloorColor        = new(0.22f, 0.13f, 0.07f); // dark mahogany
	private static readonly Color CeilingColor      = new(0.92f, 0.89f, 0.80f); // cream plaster
	private static readonly Color WallUpper         = new(0.18f, 0.26f, 0.16f); // forest green wallpaper
	private static readonly Color WainscotColor     = new(0.20f, 0.12f, 0.06f); // dark oak wainscoting
	private static readonly Color ChesterfieldColor = new(0.32f, 0.08f, 0.06f); // deep burgundy leather
	private static readonly Color WoodColor         = new(0.28f, 0.16f, 0.07f); // furniture wood
	private static readonly Color StoneColor        = new(0.55f, 0.52f, 0.48f); // fireplace stone
	private static readonly Color RugRed            = new(0.52f, 0.12f, 0.10f); // Persian rug
	private static readonly Color BookColor1        = new(0.55f, 0.22f, 0.10f); // leather books
	private static readonly Color BookColor2        = new(0.18f, 0.28f, 0.38f); // blue books
	private static readonly Color GoldColor         = new(0.72f, 0.58f, 0.18f); // brass / gold trim

	public override void _Ready()
	{
		_rng.Randomize();
		BuildRoom();
		SpawnFurniture();
		SetupLighting();
	}

	private void BuildRoom()
	{
		float hw = RoomWidth / 2.0f;
		float hd = RoomDepth / 2.0f;
		float wt = WallThickness;
		float wh = RoomHeight;

		// Floor — dark mahogany hardwood
		CreateBox("Floor", new Vector3(0, -wt / 2f, 0),
			new Vector3(RoomWidth, wt, RoomDepth), FloorColor, roughness: 0.6f);

		// Ceiling — cream plaster
		CreateBox("Ceiling", new Vector3(0, wh + wt / 2f, 0),
			new Vector3(RoomWidth, wt, RoomDepth), CeilingColor, roughness: 0.9f);

		// --- Walls: upper (wallpaper) + lower wainscoting strip ---
		float wainscotH = 1.4f; // height of dark wood panel at base of each wall
		float upperH    = wh - wainscotH;

		// North wall
		BuildWallWithWainscot("North", new Vector3(0, 0, -hd), RoomWidth, wh, wainscotH, upperH, wt, facingZ: true);
		// South wall
		BuildWallWithWainscot("South", new Vector3(0, 0,  hd), RoomWidth, wh, wainscotH, upperH, wt, facingZ: true);
		// East wall
		BuildWallWithWainscot("East",  new Vector3( hw, 0, 0), RoomDepth, wh, wainscotH, upperH, wt, facingZ: false);
		// West wall
		BuildWallWithWainscot("West",  new Vector3(-hw, 0, 0), RoomDepth, wh, wainscotH, upperH, wt, facingZ: false);

		// Crown moulding strip at ceiling join — thin gold band
		float moulding = 0.12f;
		CreateBox("MouldingN", new Vector3(0, wh - moulding / 2f, -hd + wt / 2f),
			new Vector3(RoomWidth, moulding, moulding), GoldColor, roughness: 0.3f);
		CreateBox("MouldingS", new Vector3(0, wh - moulding / 2f,  hd - wt / 2f),
			new Vector3(RoomWidth, moulding, moulding), GoldColor, roughness: 0.3f);
		CreateBox("MouldingE", new Vector3( hw - wt / 2f, wh - moulding / 2f, 0),
			new Vector3(moulding, moulding, RoomDepth), GoldColor, roughness: 0.3f);
		CreateBox("MouldingW", new Vector3(-hw + wt / 2f, wh - moulding / 2f, 0),
			new Vector3(moulding, moulding, RoomDepth), GoldColor, roughness: 0.3f);

		// Persian rug — flat slab on the floor
		CreateBox("Rug", new Vector3(0, 0.02f, 1.0f),
			new Vector3(10.0f, 0.04f, 8.0f), RugRed, roughness: 0.95f);
		// Rug border strip (gold trim)
		CreateBox("RugBorder", new Vector3(0, 0.025f, 1.0f),
			new Vector3(10.4f, 0.02f, 8.4f), GoldColor, roughness: 0.7f);
	}

	private void BuildWallWithWainscot(string prefix, Vector3 basePos, float width, float wallH,
		float wainscotH, float upperH, float wt, bool facingZ)
	{
		// Wainscoting (lower dark wood panel)
		var wainscotPos = basePos + Vector3.Up * (wainscotH / 2f);
		var wainscotSize = facingZ
			? new Vector3(width, wainscotH, wt)
			: new Vector3(wt, wainscotH, width);
		CreateBox(prefix + "Wainscot", wainscotPos, wainscotSize, WainscotColor, roughness: 0.5f);

		// Upper wall (wallpaper)
		var upperPos = basePos + Vector3.Up * (wainscotH + upperH / 2f);
		var upperSize = facingZ
			? new Vector3(width, upperH, wt)
			: new Vector3(wt, upperH, width);
		CreateBox(prefix + "Upper", upperPos, upperSize, WallUpper, roughness: 0.85f);

		// Chair rail — thin gold strip between wainscot and upper
		var railPos = basePos + Vector3.Up * wainscotH;
		var railSize = facingZ
			? new Vector3(width, 0.08f, wt * 1.2f)
			: new Vector3(wt * 1.2f, 0.08f, width);
		CreateBox(prefix + "Rail", railPos, railSize, GoldColor, roughness: 0.3f);
	}

	private void SpawnFurniture()
	{
		float hw = RoomWidth / 2.0f;
		float hd = RoomDepth / 2.0f;

		// --- Fireplace (north wall, center) ---
		SpawnFireplace(new Vector3(0, 0, -hd + WallThickness));

		// --- Chesterfield sofa (south area, facing north) ---
		SpawnChesterfield(new Vector3(0, 0, hd - 3.5f));

		// --- Two wingback chairs (flanking the rug) ---
		SpawnWingbackChair(new Vector3(-5.5f, 0, 0.5f));
		SpawnWingbackChair(new Vector3( 5.5f, 0, 0.5f));

		// --- Large bookcase (east wall) ---
		SpawnBookcase(new Vector3(hw - 1.0f, 0, -3.0f));
		SpawnBookcase(new Vector3(hw - 1.0f, 0,  3.0f));

		// --- Writing desk (west wall) ---
		SpawnWritingDesk(new Vector3(-hw + 1.8f, 0, -2.0f));

		// --- Grandfather clock (east wall, far corner) ---
		SpawnGrandfatherClock(new Vector3(hw - 0.45f, 0, hd - 1.0f));

		// --- Side tables flanking sofa ---
		SpawnSideTable(new Vector3(-1.8f, 0, hd - 3.5f));
		SpawnSideTable(new Vector3( 1.8f, 0, hd - 3.5f));

		// --- Ornate display cabinet (west wall) ---
		SpawnDisplayCabinet(new Vector3(-hw + 1.2f, 0, 4.0f));
	}

	private void SpawnFireplace(Vector3 pos)
	{
		// Stone surround
		CreateBox("FP_Left",   pos + new Vector3(-1.3f, 1.2f, 0.2f), new Vector3(0.35f, 2.4f, 0.5f), StoneColor);
		CreateBox("FP_Right",  pos + new Vector3( 1.3f, 1.2f, 0.2f), new Vector3(0.35f, 2.4f, 0.5f), StoneColor);
		CreateBox("FP_Top",    pos + new Vector3( 0,    2.5f, 0.2f), new Vector3(3.0f,  0.35f, 0.5f), StoneColor);
		// Mantelpiece (dark wood shelf)
		CreateBox("FP_Mantel", pos + new Vector3(0, 2.75f, 0.35f), new Vector3(3.6f, 0.12f, 0.7f), WoodColor);
		// Firebox (dark recess)
		CreateBox("FP_Box",    pos + new Vector3(0, 0.7f, 0.18f), new Vector3(2.0f, 1.4f, 0.1f),
			new Color(0.08f, 0.06f, 0.05f));
		// Hearth slab
		CreateBox("FP_Hearth", pos + new Vector3(0, 0.03f, 0.7f), new Vector3(2.6f, 0.06f, 1.0f), StoneColor);
		// Mantel decorations (small boxes as ornaments)
		CreateBox("FP_Orn1", pos + new Vector3(-1.0f, 2.9f, 0.35f), new Vector3(0.2f, 0.35f, 0.2f), GoldColor);
		CreateBox("FP_Orn2", pos + new Vector3( 1.0f, 2.9f, 0.35f), new Vector3(0.2f, 0.35f, 0.2f), GoldColor);
		CreateBox("FP_Clock", pos + new Vector3(0, 2.9f, 0.35f),    new Vector3(0.4f, 0.4f, 0.2f),  WoodColor);
	}

	private void SpawnChesterfield(Vector3 pos)
	{
		// Seat
		CreateBox("Sofa_Seat", pos + new Vector3(0, 0.42f, 0), new Vector3(3.0f, 0.5f, 1.0f), ChesterfieldColor);
		// Back
		CreateBox("Sofa_Back", pos + new Vector3(0, 0.9f, -0.45f), new Vector3(3.0f, 0.75f, 0.2f), ChesterfieldColor);
		// Arms
		CreateBox("Sofa_ArmL", pos + new Vector3(-1.5f, 0.72f, 0), new Vector3(0.2f, 0.65f, 1.0f), ChesterfieldColor);
		CreateBox("Sofa_ArmR", pos + new Vector3( 1.5f, 0.72f, 0), new Vector3(0.2f, 0.65f, 1.0f), ChesterfieldColor);
		// Feet
		for (int i = -1; i <= 1; i += 2)
			for (int j = -1; j <= 1; j += 2)
				CreateBox($"Sofa_Leg{i}{j}", pos + new Vector3(i * 1.3f, 0.1f, j * 0.4f),
					new Vector3(0.1f, 0.2f, 0.1f), WoodColor);
	}

	private void SpawnWingbackChair(Vector3 pos)
	{
		CreateBox("Chair_Seat", pos + new Vector3(0, 0.42f, 0), new Vector3(0.85f, 0.4f, 0.85f), ChesterfieldColor);
		CreateBox("Chair_Back", pos + new Vector3(0, 0.95f, -0.4f), new Vector3(0.85f, 1.0f, 0.15f), ChesterfieldColor);
		// Winged sides on back
		CreateBox("Chair_WingL", pos + new Vector3(-0.4f, 1.05f, -0.25f), new Vector3(0.12f, 0.6f, 0.35f), ChesterfieldColor);
		CreateBox("Chair_WingR", pos + new Vector3( 0.4f, 1.05f, -0.25f), new Vector3(0.12f, 0.6f, 0.35f), ChesterfieldColor);
		CreateBox("Chair_ArmL",  pos + new Vector3(-0.4f, 0.65f, 0.1f),  new Vector3(0.1f, 0.2f, 0.65f), ChesterfieldColor);
		CreateBox("Chair_ArmR",  pos + new Vector3( 0.4f, 0.65f, 0.1f),  new Vector3(0.1f, 0.2f, 0.65f), ChesterfieldColor);
	}

	private void SpawnBookcase(Vector3 pos)
	{
		// Frame
		CreateBox("BC_Frame", pos, new Vector3(0.4f, 3.2f, 2.0f), WoodColor);
		// Book rows — random colored spines
		for (int row = 0; row < 4; row++)
		{
			float y = 0.5f + row * 0.65f;
			var bookCol = (row % 2 == 0) ? BookColor1 : BookColor2;
			CreateBox($"BC_Books{row}", pos + new Vector3(-0.12f, y, 0),
				new Vector3(0.18f, 0.5f, 1.7f), bookCol);
		}
	}

	private void SpawnWritingDesk(Vector3 pos)
	{
		CreateBox("Desk_Top",  pos + new Vector3(0, 0.78f, 0), new Vector3(0.7f, 0.06f, 1.8f), WoodColor);
		CreateBox("Desk_DrawL", pos + new Vector3(0, 0.4f, -0.5f), new Vector3(0.65f, 0.7f, 0.8f), WoodColor);
		CreateBox("Desk_DrawR", pos + new Vector3(0, 0.4f,  0.5f), new Vector3(0.65f, 0.7f, 0.8f), WoodColor);
		// Inkwell / papers (tiny decorations)
		CreateBox("Desk_Ink", pos + new Vector3(-0.1f, 0.83f, -0.5f), new Vector3(0.08f, 0.1f, 0.08f), GoldColor);
	}

	private void SpawnGrandfatherClock(Vector3 pos)
	{
		CreateBox("Clock_Base",  pos + new Vector3(0, 0.3f,  0),   new Vector3(0.7f, 0.6f,  0.5f), WoodColor);
		CreateBox("Clock_Body",  pos + new Vector3(0, 1.2f,  0),   new Vector3(0.55f, 1.2f, 0.4f), WoodColor);
		CreateBox("Clock_Hood",  pos + new Vector3(0, 2.1f,  0),   new Vector3(0.65f, 0.6f, 0.45f), WoodColor);
		CreateBox("Clock_Face",  pos + new Vector3(-0.22f, 1.7f, 0), new Vector3(0.04f, 0.42f, 0.36f),
			new Color(0.88f, 0.84f, 0.72f));
	}

	private void SpawnSideTable(Vector3 pos)
	{
		CreateBox("ST_Top", pos + new Vector3(0, 0.55f, 0), new Vector3(0.7f, 0.06f, 0.7f), WoodColor);
		for (int i = -1; i <= 1; i += 2)
			for (int j = -1; j <= 1; j += 2)
				CreateBox($"ST_Leg{i}{j}", pos + new Vector3(i * 0.28f, 0.27f, j * 0.28f),
					new Vector3(0.07f, 0.54f, 0.07f), WoodColor);
	}

	private void SpawnDisplayCabinet(Vector3 pos)
	{
		CreateBox("DC_Frame",  pos, new Vector3(0.4f, 2.4f, 1.6f), WoodColor);
		CreateBox("DC_Glass",  pos + new Vector3(-0.16f, 0.4f, 0), new Vector3(0.05f, 1.6f, 1.4f),
			new Color(0.7f, 0.85f, 0.8f, 0.3f));
		// Decorative items on shelves
		for (int shelf = 0; shelf < 3; shelf++)
			CreateBox($"DC_Item{shelf}", pos + new Vector3(-0.12f, 0.5f + shelf * 0.55f, _rng.RandfRange(-0.4f, 0.4f)),
				new Vector3(0.12f, 0.18f, 0.12f), GoldColor);
	}

	private void SetupLighting()
	{
		var env = new WorldEnvironment();
		var envRes = new Godot.Environment();
		envRes.BackgroundMode = Godot.Environment.BGMode.Color;
		envRes.BackgroundColor = new Color(0.04f, 0.03f, 0.02f);
		envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		envRes.AmbientLightColor = new Color(0.28f, 0.22f, 0.14f); // warm amber fill
		envRes.AmbientLightEnergy = 0.4f;
		envRes.SsaoEnabled = true;
		envRes.GlowEnabled = true;
		envRes.GlowIntensity = 0.5f;
		env.Environment = envRes;
		AddChild(env);

		// Central chandelier — warm gas-light amber
		var chandelier = new OmniLight3D();
		chandelier.Name = "Chandelier";
		chandelier.Position = new Vector3(0, RoomHeight - 0.6f, 0);
		chandelier.OmniRange = 22.0f;
		chandelier.LightEnergy = 1.8f;
		chandelier.LightColor = new Color(1.0f, 0.82f, 0.55f);
		chandelier.ShadowEnabled = true;
		AddChild(chandelier);

		// Chandelier fixture (decorative box)
		CreateBox("ChandelierFix", new Vector3(0, RoomHeight - 0.3f, 0),
			new Vector3(0.5f, 0.3f, 0.5f), GoldColor);

		// Fireplace glow — orange flicker
		var fireplaceGlow = new OmniLight3D();
		fireplaceGlow.Name = "FireplaceGlow";
		fireplaceGlow.Position = new Vector3(0, 0.8f, -RoomDepth / 2.0f + 1.2f);
		fireplaceGlow.OmniRange = 9.0f;
		fireplaceGlow.LightEnergy = 1.4f;
		fireplaceGlow.LightColor = new Color(1.0f, 0.45f, 0.1f);
		AddChild(fireplaceGlow);

		// Wall sconces — warm side lights (4 corners)
		SpawnSconce(new Vector3(-RoomWidth / 2f + 0.3f,  3.2f, -RoomDepth / 4f));
		SpawnSconce(new Vector3(-RoomWidth / 2f + 0.3f,  3.2f,  RoomDepth / 4f));
		SpawnSconce(new Vector3( RoomWidth / 2f - 0.3f,  3.2f, -RoomDepth / 4f));
		SpawnSconce(new Vector3( RoomWidth / 2f - 0.3f,  3.2f,  RoomDepth / 4f));

		// Ominous monster spotlight — red tinge from north
		var monsterGlow = new OmniLight3D();
		monsterGlow.Name = "MonsterGlow";
		monsterGlow.Position = new Vector3(0, 1.5f, -5.0f);
		monsterGlow.OmniRange = 10.0f;
		monsterGlow.LightEnergy = 0.8f;
		monsterGlow.LightColor = new Color(0.9f, 0.18f, 0.08f);
		AddChild(monsterGlow);
	}

	private void SpawnSconce(Vector3 pos)
	{
		var sconce = new OmniLight3D();
		sconce.Position = pos;
		sconce.OmniRange = 6.0f;
		sconce.LightEnergy = 0.6f;
		sconce.LightColor = new Color(1.0f, 0.78f, 0.45f);
		AddChild(sconce);

		// Sconce bracket
		CreateBox("Sconce", pos + new Vector3(0.15f, 0, 0), new Vector3(0.15f, 0.25f, 0.15f), GoldColor);
	}

	private void CreateBox(string name, Vector3 position, Vector3 size, Color color, float roughness = 0.85f)
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
		mat.Roughness = roughness;
		mesh.MaterialOverride = mat;
		body.AddChild(mesh);

		var col = new CollisionShape3D();
		col.Shape = new BoxShape3D { Size = size };
		body.AddChild(col);

		AddChild(body);
	}
}
