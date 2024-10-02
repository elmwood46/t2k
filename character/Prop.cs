using Godot;
using System;

// Abstract class to represent a prop in the game. A prop is any non-environmental map entity that can be interacted with and highlighted with the mouse.
// extend this class for scripts attached to CharacterBody3D nodes that will be actors on the map
// props have physics properties, an AnimatedSprite3D, and a spherical collision shape.

public abstract partial class Prop : CharacterBody3D
{
    [Export] // string title of this pawn
    public string Title { get; set; } = "default";

    [Export]
    public bool Highlight { get; set; }
    private ShaderMaterial _hshader_mater = ResourceLoader.Load("res://shaders/edgeHighlightShaderMat.tres") as ShaderMaterial;

    #region phsyics variables [need to fill in]
    public const float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;
    #endregion


    #region util
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
            else
            {
                throw new Exception("AnimatedSprite3D not found for prop: " + this.Name);
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
            else
            {
                throw new Exception("AnimatedSprite3D not found for prop: " + this.Name);
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
            else
            {
                throw new Exception("AnimatedSprite3D not found for prop: " + this.Name);
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
            else
            {
                throw new Exception("CollisionShape3D not found for prop: " + this.Name);
            }
        }
    }
    private float _collisionRadius = 0.5f; // Default radius value
    #endregion

    public void UpdateOutline()
    {
        // Get the AnimatedSprite3D node

        var sprite = GetNodeOrNull<AnimatedSprite3D>("%AnimatedSprite3D");
        if (sprite != null)
        {
            if (Highlight)
            {

                // Get the texture of the current frame
                Texture2D currentFrameTexture = sprite.SpriteFrames.GetFrameTexture(sprite.Animation, sprite.Frame);

                // Assign the texture to the ShaderMaterial
                _hshader_mater.SetShaderParameter("texture_albedo", currentFrameTexture);
                _hshader_mater.SetShaderParameter("y_billboard", SpriteYBillboard);

                // Apply the ShaderMaterial to the sprite's MaterialOverride
                sprite.MaterialOverride = _hshader_mater;
            }
            else
            {
                // Remove the ShaderMaterial from the sprite's MaterialOverride
                sprite.MaterialOverride = null;
            }
        }
        else
        {
            throw new Exception("Highlight failure. AnimatedSprite3D not found for prop: " + this.Name);
        }
    }
}