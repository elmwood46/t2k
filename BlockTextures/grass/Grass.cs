using Godot;

[Tool]
public partial class Grass : MultiMeshInstance3D {
    [Export] public float Density
    {
        get => _density;
        set
        {
            if (value < 1.0f) value = 1.0f;
            _density = value;
            Rebuild(); // Automatically call Rebuild when Span is changed in the editor
        }
    }
    private float _density = 5000.0f;

    [Export] public Vector2 BladeWidth
    {
        get => _width;
        set
        {
            _width = value;
            Rebuild(); // Automatically call Rebuild when Width is changed in the editor
        }
    }
    private Vector2 _width = new (0.01f, 0.02f);

    [Export] public Vector2 BladeHeight
    {
        get => _height;
        set
        {
            _height = value;
            Rebuild(); // Automatically call Rebuild when Height is changed in the editor
        }
    }
    private Vector2 _height = new (0.04f, 0.08f);   

    [Export] public Vector2 SwayYawDegrees
    {
        get => _swayYawDegrees;
        set
        {
            _swayYawDegrees = value;
            Rebuild(); // Automatically call Rebuild when SwayYawDegrees is changed in the editor
        }
    }
    private Vector2 _swayYawDegrees = new (0.0f, 10.0f);

    [Export] public Vector2 SwayPitchDegrees
    {
        get => _swayPitchDegrees;
        set
        {
            _swayPitchDegrees = value;
            Rebuild(); // Automatically call Rebuild when SwayPitchDegrees is changed in the editor
        }
    }
    private Vector2 _swayPitchDegrees = new (0.04f, 0.08f);

    [Export] public Mesh TerrainMesh {
        get => _terrainMesh;
        set
        {
            _terrainMesh = value;
            Rebuild(); // Automatically call Rebuild when TerrainMesh is changed in the editor
        }
    }
    private Mesh _terrainMesh;

    public Grass() {
        Rebuild();
    }

    public void Rebuild() {
        Multimesh ??= new MultiMesh();
        Multimesh.InstanceCount = 0;
        if (TerrainMesh == null) return;
        Multimesh.Mesh = MeshFactory.SimpleGrass();
        Multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        Multimesh.UseCustomData = true;
        Multimesh.UseColors = false;
        
        var spawns = GrassFactory.Generate(TerrainMesh, Density, BladeWidth, BladeHeight, SwayYawDegrees, SwayPitchDegrees);

        if (spawns.Count == 0) return;

	    Multimesh.InstanceCount = spawns.Count;
        for (int i=0;i<Multimesh.InstanceCount;i++) {
            var spawn = spawns[i];
            Multimesh.SetInstanceTransform(i, spawn.Item1);
            Multimesh.SetInstanceCustomData(i, spawn.Item2);
        }
    }
}