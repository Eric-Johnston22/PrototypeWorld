using Godot;

namespace Hoarders;

/// <summary>
/// Component script for objects that can be sucked up by the vacuum.
/// Attach to a RigidBody3D. The object must be in the "vacuumable" group.
/// </summary>
public partial class VacuumableObject : RigidBody3D
{
	public enum ObjectSize { Small, Medium, Large }

	[Export] public ObjectSize Size = ObjectSize.Small;
	[Export] public string DisplayName = "Junk";
	[Export] public Color ObjectColor = Colors.White;

	private Vector3 _originalScale;
	private MeshInstance3D _meshInstance;

	public override void _Ready()
	{
		AddToGroup("vacuumable");
		_originalScale = Scale;
		SetMeta("display_name", DisplayName);

		// Apply size-based physics properties
		switch (Size)
		{
			case ObjectSize.Small:
				Mass = 0.5f;
				break;
			case ObjectSize.Medium:
				Mass = 2.0f;
				break;
			case ObjectSize.Large:
				Mass = 8.0f;
				break;
		}

		// Apply color to the mesh if we have one
		_meshInstance = FindMeshChild(this);
		if (_meshInstance != null)
		{
			var mat = new StandardMaterial3D();
			mat.AlbedoColor = ObjectColor;
			mat.Roughness = 0.7f;
			mat.Metallic = 0.1f;
			_meshInstance.MaterialOverride = mat;
		}
	}

	private static MeshInstance3D FindMeshChild(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is MeshInstance3D mesh)
				return mesh;
			var found = FindMeshChild(child);
			if (found != null)
				return found;
		}
		return null;
	}
}
