using Godot;
using RandomNumberGenerator = Godot.RandomNumberGenerator;

public enum DestructibleMeshType {
    Stone,
    Treasure,
    Cube
}

public interface IHurtable
{
	void TakeDamage(int damage, BlockDamageType type);
}

/// <summary>
/// Destructible mesh is designed to work with a specific scene setup.
/// You need a root node3d with the destructible mesh script (making the Node3D of class DestructibleMesh).
/// The DestructibleMesh node has two children scene nodes, one for the intact scene and one for the broken.
/// Each child scene is a node3d which holds physics bodies.
/// The intact scene's root is a node 3d and its first (and only) child is a physicsbody3d (either static or rigid) with the intact mesh.
/// The physicsbody3d's first child must be the meshinstance3d which holds its mesh and material data.
/// The broken scene's root is a node3d and it has a number of rigid bodies as children which are the fragmented pieces of the intact scene.
/// When the intact scene is broken, it is freed from the queue and the broken scene is made visible, its collisions are enabled, and it is moved to the position occupied by the intact scene.
/// </summary>
public partial class DestructibleMesh : Node3D, IHurtable
{
	[Export] public PackedScene IntactPacked {get ; set;}
	[Export] public PackedScene BrokenPacked {get; set;}
	[Export] public Node3D IntactScene { get; set; }
	[Export] public Node3D BrokenScene { get; set; }
	[Export] public float DecayTime { get; set; } = 3.0f;
	[Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public DestructibleMeshType Type {get;set;}

	public Texture2D Texture { get; set; }
	public float Health = -1.0f;
    public int PackedBlockDamageInfo = 0;
	private ShaderMaterial _shaderMaterial;

	private Vector3 _base_scale;
	private Vector3 _base_position;
	private double _max_hit_time = 0.5f;
	private double _hit_time = 0.0;
	private float _hit_anim_lerp = 0.1f;
	private float _max_scale_factor = 1.05f;
	private float _max_shake_factor = 0.05f;
	private float _shake_factor = 0.0f;

    private bool _is_broken = false;


	private static readonly PackedScene _break_particles = ResourceLoader.Load<PackedScene>("res://props/stones/break_object_particles.tscn");
    private static readonly Texture2D _stone_texture =  ResourceLoader.Load<Texture2D>("res://props/stones/Textures/T_Stone.png");
    private static readonly Texture2D _cube_texture = ResourceLoader.Load<Texture2D>("res://BlockTextures/EMPTY_TEXTURE.png");

	private static readonly RandomNumberGenerator RNG = new();

	private Timer _death_timer = new(){Autostart = false};

	public override void _Ready()
	{
        CallDeferred(MethodName.AddChild,_death_timer);
        Texture = Type switch {
            DestructibleMeshType.Stone => _stone_texture,
            _ => _cube_texture
        };

		_base_scale = ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale;
		_base_position = ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Position;
		if (Health == -1) Health = MaxHealth;
		BrokenScene.Visible = false;
		_shaderMaterial = (ShaderMaterial)BlockManager.Instance.DestructibleObjectShader.Duplicate();
		_shaderMaterial.SetShaderParameter("_texture_albedo", Texture);
        _shaderMaterial.SetShaderParameter("_damage_data", ChunkManager.GetBlockDamageData(PackedBlockDamageInfo));
		if (IntactScene.GetChild(0).GetChild(0) is MeshInstance3D m) {
			m.MaterialOverride = _shaderMaterial;
		}
	}

	public void SetIntactMeshBaseScaleAndPosition(Vector3 base_scale, Vector3 base_position)
	{
		_base_scale = base_scale;
		_base_position = base_position;
	}

	public void TakeDamage(int damage, BlockDamageType type)
	{
		Health -= damage;
		if (Health <= 0) Health = 0;
		var healthfact = 1.0f-Health/MaxHealth;
        var new_dmg = Mathf.RoundToInt(BlockManager.BLOCK_BREAK_DAMAGE_THRESHOLD*healthfact);
        PackedBlockDamageInfo = ChunkManager.AddBlockDamage(PackedBlockDamageInfo, type, new_dmg);
		GD.Print($"set damage to: {Mathf.RoundToInt(BlockManager.BLOCK_BREAK_DAMAGE_THRESHOLD*healthfact)}");
		_shaderMaterial.SetShaderParameter("_damage_data", ChunkManager.GetBlockDamageData(PackedBlockDamageInfo));

        // do shake effect
        // only shake if we took damage and are a static body
        // (rigid bodies already react to damage by being shoved, we don't need to do anything here)
		if (damage <= 0 || IntactScene.GetChild(0) is not RigidBody3D rb || rb.Freeze == false) return;
		_hit_time = _max_hit_time;


		float scalex, scaley, scalez;
		scalex = Mathf.Lerp(_base_scale.X,_base_scale.X*_max_scale_factor,healthfact);
		scaley = Mathf.Lerp(_base_scale.Y,_base_scale.Y*_max_scale_factor,healthfact);
		scalez = Mathf.Lerp(_base_scale.Z,_base_scale.Z*_max_scale_factor,healthfact);
		((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale = new Vector3(scalex,scaley,scalez);

		_shake_factor = Mathf.Lerp(0.0f,_max_shake_factor,healthfact);
	}

	public void Deactivate()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void Reactivate()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Pausable;
	}

	public void Break(Vector3 collisionPoint, float force) {
        if (_is_broken) return;
        _is_broken = true;
        if (((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale != _base_scale) ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale = _base_scale;
        if (((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Position != _base_position) ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Position = _base_position;
        // spawn coins
        var spawn_chance = Type switch {
            DestructibleMeshType.Stone => 0.10f,
            DestructibleMeshType.Treasure => 1.0f,
            DestructibleMeshType.Cube => 1.0f,
            _ => 0.0f
        };
        if (RNG.Randf() < spawn_chance)
        {
            BrokenScene.GetChild(0).AddChild(CoinSpawner.Create(((Node3D)IntactScene.GetChild(0)).GlobalPosition,100,2.0));
        }

		var part = _break_particles.Instantiate() as GpuParticles3D;
		AddChild(part);
		part.Emitting = true;
		if (!BrokenScene.Visible) {
			BrokenScene.Position = ((Node3D)IntactScene.GetChild(0)).Position;
			BrokenScene.Rotation = ((Node3D)IntactScene.GetChild(0)).Rotation;
			BrokenScene.Visible = true;

			if (IntactScene.GetChild(0) is PhysicsBody3D pb)
			{
				pb.SetCollisionLayerValue(1, false);
				pb.SetCollisionMaskValue(1, false);
				pb.SetCollisionLayerValue(2, false);
				pb.SetCollisionMaskValue(2, false);
			}

			foreach (Node child in BrokenScene.GetChildren()) {
				if (child is RigidBody3D rb) {
					if (rb.GetChild(0) is MeshInstance3D m)
					{
						m.MaterialOverride = _shaderMaterial;
					}
					rb.SetCollisionLayerValue(1, false);
					rb.SetCollisionLayerValue(2, true);
					rb.SetCollisionMaskValue(1, true);
					rb.SetCollisionMaskValue(2, true);
					rb.Freeze = false;
					var force_dir = collisionPoint.DirectionTo(rb.GlobalPosition);
					if (IntactScene.GetChild(0) is RigidBody3D intact_rb) {
						rb.LinearVelocity = intact_rb.LinearVelocity;
					}
					rb.ApplyImpulse(force_dir*force);
				}
			}

			IntactScene.Visible = false;
			IntactScene.QueueFree();

			var _t = new Timer
			{
				Autostart = false,
				WaitTime = DecayTime*0.8,
                OneShot = true
			};
			_t.Timeout += () => {
                _death_timer = new Timer() {
                    Autostart = false,
                    OneShot = true,
                    WaitTime = DecayTime*0.2
                };
                AddChild(_death_timer);
                _death_timer.Timeout += () => {
				    CallDeferred(MethodName.QueueFree);
                };
                _death_timer.Start();
			};
			AddChild(_t);
			_t.Start();
		}
	}

    float _frame = 0;
	public override void _PhysicsProcess(double delta)
	{
        // scale broken fragments
		if (_is_broken) {
            if (!_death_timer.IsStopped()) {
                foreach (Node child in BrokenScene.GetChildren()) {
                    if (child is RigidBody3D rb) {
                        ((MeshInstance3D)rb.GetChild(0)).Scale = (float)Mathf.Max(_death_timer.TimeLeft/_death_timer.WaitTime,0.1)*Vector3.One;
                        ((CollisionShape3D)rb.GetChild(1)).Scale = (float)Mathf.Max(_death_timer.TimeLeft/_death_timer.WaitTime,0.1)*Vector3.One;
                    }
                }
            }
		}
		else
		{
            // do shake effect
			if (_hit_time > 0) 
			{
				// lerp scale back to normal
				var scale = ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale;
				float scalex, scaley, scalez;
				scalex = Mathf.Lerp(scale.X,_base_scale.X,_hit_anim_lerp);
				scaley = Mathf.Lerp(scale.Y,_base_scale.Y,_hit_anim_lerp);
				scalez = Mathf.Lerp(scale.Z,_base_scale.Z,_hit_anim_lerp);
				((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale = new Vector3(scalex,scaley,scalez);

				var sf = _shake_factor*(float)(_hit_time / _max_hit_time);
				var randvec = new Vector3(RNG.RandfRange(-sf,sf),RNG.RandfRange(-sf,sf),RNG.RandfRange(-sf,sf));

				((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Position = _base_position + randvec * Mathf.Sin((float)_hit_time);

				_hit_time-=delta;
			}
			else 
			{
				if (((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale != _base_scale) ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Scale = _base_scale;
				if (((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Position != _base_position) ((MeshInstance3D)IntactScene.GetChild(0).GetChild(0)).Position = _base_position;				
			}
		}
	}
}