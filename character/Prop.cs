using Godot;
using System;

// Abstract class to represent a prop in the game. A prop is any non-environmental map entity that can be interacted with and highlighted with the mouse.
// extend this class for scripts attached to CharacterBody3D nodes that will be actors on the map
// props have physics properties, an AnimatedSprite3D, and a spherical collision shape.

public abstract partial class Prop : CharacterBody3D
{
    [Export] public AnimatedSprite3D Sprite  { get; set; }

    [Export] public CollisionShape3D CollisionShape { get; set; }

    [Export] public NavigationAgent3D Nav { get; set; }

    [Export] public string Title { get; set; } = "default";

    [Export] public bool Highlight { get; set; }
    private ShaderMaterial _hshader_mater = ResourceLoader.Load("res://shaders/edgeHighlightShaderMat.tres") as ShaderMaterial;

    #region phsyics variables [need to fill in]
    public const float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;
    #endregion

    //<description>
    // SpriteFrames property for the pawn. This is the sprite sheet that will be used to render the pawn.
    ///</description>
    [Export] public SpriteFrames SpriteFrames
    {
        get => _spriteFrames;
        set
        {
            _spriteFrames = value;
            if (Sprite != null)
            {
                Sprite.SpriteFrames = _spriteFrames;
                Sprite.Play("default");
            }
            else
            {
                
                GD.Print("AnimatedSprite3D not set for prop: " + Name);
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
            if (Sprite != null)
            {
                Sprite.Offset = _spriteOffset;
            }
            else
            {
                throw new Exception("AnimatedSprite3D not set for prop: " + Name);
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
            if (Sprite != null)
            {
                Sprite.Billboard = _spriteBillboard ? BaseMaterial3D.BillboardModeEnum.FixedY : BaseMaterial3D.BillboardModeEnum.Disabled;
            }
            else
            {
                throw new Exception("AnimatedSprite3D not set for prop: " + Name);
            }
        }
    }
    private bool _spriteBillboard = true;

    [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
    public float CollisionRadius
    {
        get => _collisionRadius;
        set
        {
            _collisionRadius = value;
            _collisionRadius = Mathf.Round(_collisionRadius * 10f) / 10f;
            if (CollisionShape != null)
            {
                if (CollisionShape.Shape is SphereShape3D sphereShape)
                {
                    sphereShape.Radius = _collisionRadius;
                }
            }
            else
            {
                throw new Exception("CollisionShape3D not set for prop: " + Name);
            }
        }
    }
    private float _collisionRadius = 0.5f; // Default radius value


    public void UpdateOutline()
    {
        // Get the AnimatedSprite3D node

        if (Sprite != null)
        {
            if (Highlight)
            {

                // Get the texture of the current frame
                Texture2D currentFrameTexture = Sprite.SpriteFrames.GetFrameTexture(Sprite.Animation, Sprite.Frame);

                // Assign the texture to the ShaderMaterial
                _hshader_mater.SetShaderParameter("texture_albedo", currentFrameTexture);
                _hshader_mater.SetShaderParameter("y_billboard", SpriteYBillboard);

                // Apply the ShaderMaterial to the sprite's MaterialOverride
                Sprite.MaterialOverride = _hshader_mater;
            }
            else
            {
                // Remove the ShaderMaterial from the sprite's MaterialOverride
                Sprite.MaterialOverride = null;
            }
        }
        else
        {
            throw new Exception("Highlight failure. AnimatedSprite3D not set for prop: " + Name);
        }
    }
}