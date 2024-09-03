using Godot;
using System;

// Abstract class to represent a pawn in the game
// tracks all the necessary variables for an actor in combat
// A pawn tracks the base stats of a character, their armour and their HP
public abstract partial class Pawn : CharacterBody3D
{

	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;

    #region util
    public bool IsHighlighted { get; set; }
    private static ShaderMaterial _highlight_shader = ResourceLoader.Load("res://shaders/edge_highlight.gdshader") as ShaderMaterial;
    public static ShaderMaterial GetHighlightShader() => _highlight_shader;

    [Export] // string title of this pawn
    public string Title { get; set; } = "default";

	[Export]
    //<description>
    // SpriteFrames property for the pawn. This is the sprite sheet that will be used to render the pawn.
    ///</description>
	public SpriteFrames SpriteFrames
	{
		get => _spriteFrames;
		set
		{
			_spriteFrames = value;
			var sprite = GetNodeOrNull<AnimatedSprite3D>("%AnimatedSprite3D");
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
			var sprite = GetNodeOrNull<AnimatedSprite3D>("%AnimatedSprite3D");
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
			var sprite = GetNodeOrNull<AnimatedSprite3D>("%AnimatedSprite3D");
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
		get => this._collisionRadius;
		set
		{
			this._collisionRadius = value;
			this._collisionRadius = Mathf.Round(this._collisionRadius * 10f) / 10f;
			var collisionShape = GetNodeOrNull<CollisionShape3D>("%CollisionShape3D");
			if (collisionShape != null)
			{
				if (collisionShape.Shape is SphereShape3D sphereShape)
				{
					sphereShape.Radius = this._collisionRadius;
				}
			}
		}
	}
	private float _collisionRadius = 0.5f; // Default radius value
    #endregion


    #region attributes
    [Export]
    public int Mus { get; set; }
    [Export]
    public int Bra { get; set; }
    [Export]
    public int Riz { get; set; }
    [Export]
    public int Gno { get; set; }
    [Export]
    public int Money {get; set;}

    public void SetAttr(int mus, int bra, int riz, int gno)
    {
        Mus = mus;
        Bra = bra;
        Riz = riz;
        Gno = gno;
    }
    #endregion

    #region stats
    [ExportGroup("Set Stats Manually (-1 for automatic)")]
    [Export]
    public int SetAtk { get; set; } = -1;
    [Export]
    public int SetDef { get; set; } = -1;
    [Export]
    public int SetSav { get; set; } = -1;
    [Export]
    public int SetWard { get; set; } = -1;
    [Export]
    public int SetMov { get; set; } = -1;
    [Export]
    public int SetMaxHp { get; set; } = -1;
    public int Atk { get; private set; } = 0;
    // def - reduces the physical damage taken each hit. decreases over time.

    public int Def { get; private set; } = 0;
    // saving throw - used to resist bad status effects

    public int Sav { get; private set; } = 0;
    // ward - reduces the damage taken from spells and ranged attacks

    public int Ward { get; private set; } = 0;
    // move - how many map units a pawn can move on their turn

    public int Mov { get; private set; } = 0;

    public int HP { get; private set; } = 1;

    public int MaxHP { get; private set; } = 1;

    public void UpdateAllStats() {
        UpdateHealth(SetMaxHp);
        UpdateAtk(SetAtk);
        UpdateDef(SetDef);
        UpdateSav(SetSav);
        UpdateWard(SetWard);
        UpdateMov(SetMov);
    }
    #endregion

    #region combat methods
    public void TakeDamage(int damage)
    {
        HP -= damage;
        if (HP <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        // Implement death logic here
        GD.Print(Title, ": I'm dead!");
        QueueFree();
    }

    public void Heal(int amount)
    {
        HP += amount;
        if (HP > MaxHP)
        {
            HP = MaxHP;
        }
    }
    #endregion

    #region setters for stats
    private void UpdateHealth(int value)
    {
        if (value >= 0)
        {
            MaxHP = value;
        }
        else
        {
            MaxHP = 10 + Mus * 2;
        }
        HP = MaxHP;
    }

    // Update methods for the new properties
    public void UpdateAtk(int value)
    {
        if (value >= 0)
        {
            Atk = value;
        }
        else
        {
            Atk = Bra * 2;
        }
    }

    public void UpdateDef(int value)
    {
        if (value >= 0)
        {
            Def = value;
        }
        else
        {
            Def = 0;
        }
    }

    public void UpdateSav(int value)
    {
        if (value >= 0)
        {
            Sav = value;
        }
        else
        {
            Sav = Mus + Bra + Riz + Gno;
        }
    }

    public void UpdateWard(int value)
    {
        if (value >= 0)
        {
            Ward = value;
        }
        else
        {
            Ward = 5 + Gno * 2;
        }
    }

    public void UpdateMov(int value)
    {
        if (value >= 0)
        {
            Mov = value;
        }
        else
        {
            Mov = 6 + Mus;
        }
    }
    #endregion
}