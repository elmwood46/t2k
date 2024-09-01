using Godot;

[Tool]
public partial class BasicPawn : Pawn
{
	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;

	[Export]
	public SpriteFrames SpriteFrames
	{
		get => _spriteFrames;
		set
		{
			_spriteFrames = value;
			var sprite = GetNodeOrNull<AnimatedSprite3D>("AnimatedSprite3D");
			if (sprite != null)
			{
				sprite.SpriteFrames = _spriteFrames;
				sprite.Play("default");
			}
		}
	}
	private SpriteFrames _spriteFrames;

	[Export]
	public Vector2 SpriteOffset
	{
		get => _spriteOffset;
		set
		{
			_spriteOffset = value;
			var sprite = GetNodeOrNull<AnimatedSprite3D>("AnimatedSprite3D");
			if (sprite != null)
			{
				sprite.Offset = _spriteOffset;
			}
		}
	}
	private Vector2 _spriteOffset = new Vector2(0, 22f);

	[Export]
	public bool SpriteYBillboard
	{
		get => _spriteBillboard;
		set
		{
			_spriteBillboard = value;
			var sprite = GetNodeOrNull<AnimatedSprite3D>("AnimatedSprite3D");
			if (sprite != null)
			{
				sprite.Billboard = _spriteBillboard ? BaseMaterial3D.BillboardModeEnum.FixedY : BaseMaterial3D.BillboardModeEnum.Disabled;
			}
		}
	}
	private bool _spriteBillboard = true;

	[Export(PropertyHint.Range, "0.5, 10.0, 0.1")]
	public float CollisionRadius
	{
		get => _collisionRadius;
		set
		{
			_collisionRadius = value;
			_collisionRadius = Mathf.Round(_collisionRadius * 10f) / 10f;
			var collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
			if (collisionShape != null)
			{
				if (collisionShape.Shape is SphereShape3D sphereShape)
				{
					sphereShape.Radius = _collisionRadius;
				}
			}
		}
	}
	private float _collisionRadius = 0.5f; // Default radius value

    public override void _Ready()
    {
        base._Ready(); // Calls the _Ready() method in Pawn
        GD.Print("BasicPawn _Ready called");
        // Additional initialization for BasicPawn
    }

	public override void _PhysicsProcess(double delta)
	{

	}
}
