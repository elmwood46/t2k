using Godot;
using System;

// Abstract class to represent a prop in the game. A prop is any non-environmental map entity that can be interacted with and highlighted with the mouse.
// extend this class for scripts attached to CharacterBody3D nodes that will be actors on the map
// props have physics properties, an AnimatedSprite3D, and a spherical collision shape.

public abstract partial class Prop : CharacterBody3D
{
    [Export]  public string Title { get; set; } = "default"; // string title of this pawn

    [ExportCategory("Sprite")]
    [Export] public AnimatedSprite3D Sprite {get; set;}

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
        }
    }

    [Export] public Vector2 SpriteOffset
    {
        get => _spriteOffset;
        set
        {
            _spriteOffset = value;
            if (Sprite != null)
            {
                Sprite.Offset = _spriteOffset;
            }
        }
    }

    [Export] public bool SpriteYBillboard
    {
        get => _spriteYBillboard;
        set
        {
            _spriteYBillboard = value;
            if (Sprite != null)
            {
                Sprite.Billboard = _spriteYBillboard ? BaseMaterial3D.BillboardModeEnum.FixedY : BaseMaterial3D.BillboardModeEnum.Disabled;
            }
        }
    }

    [ExportCategory("Collision")]
    [Export] public CollisionShape3D CollisionShape {get; set;}
    [Export(PropertyHint.Range, "0.05, 10.0, 0.05")] public float CollisionRadius
    {
        get => _collisionRadius;
        set
        {
            _collisionRadius = value;
            _collisionRadius = Mathf.Round(this._collisionRadius * 20f) / 20f;
            if (CollisionShape != null)
            {
                if (CollisionShape.Shape is SphereShape3D sphereShape)
                {
                    sphereShape.Radius = this._collisionRadius;
                } else if (CollisionShape.Shape is CapsuleShape3D capsuleShape)
                {
                    capsuleShape.Radius = this._collisionRadius;
                } else if (CollisionShape.Shape is CylinderShape3D boxShape)
                {
                    boxShape.Radius = this._collisionRadius;
                }
            }
        }
    }

    public Timer StunTimer { get; set; }

    // check if highlighted
    public bool IsHighlighted = false;
    // edge shader
    public static ShaderMaterial HighlightShaderMaterial = ResourceLoader.Load("res://assets/shad_highlight_obj.gdshader") as ShaderMaterial;

    // backing fields
    private SpriteFrames _spriteFrames;
    private Vector2 _spriteOffset;
    private bool _spriteYBillboard;
    private float _collisionRadius = 1.0f;

    public void UpdateOutline()
    {
        // Get the AnimatedSprite3D node
        if (Sprite != null)
        {
            if (IsHighlighted)
            {

                // Get the texture of the current frame
                var currentFrameTexture = Sprite.SpriteFrames.GetFrameTexture(Sprite.Animation, Sprite.Frame);

                // Assign the texture to the ShaderMaterial
                HighlightShaderMaterial.SetShaderParameter("texture_albedo", currentFrameTexture);
                HighlightShaderMaterial.SetShaderParameter("y_billboard", SpriteYBillboard);

                // Apply the ShaderMaterial to the sprite's MaterialOverride
                Sprite.MaterialOverride = HighlightShaderMaterial;
            }
            else
            {
                // Remove the ShaderMaterial from the sprite's MaterialOverride
                Sprite.MaterialOverride = null;
            }
        }
    }
}