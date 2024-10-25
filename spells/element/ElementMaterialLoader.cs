using Godot;
using System;

public partial class ElementMaterialLoader : Node
{
	public static Material FIRE_MATERIAL {get; private set;}
	public static Material EARTH_MATERIAL {get; private set;}

    public override void _Ready()
    {
        FIRE_MATERIAL = ResourceLoader.Load<Material>("res://spells/element/materials/FireMaterial.tres");
        EARTH_MATERIAL = ResourceLoader.Load<Material>("res://spells/element/materials/EarthMaterial.tres");
    }

}
