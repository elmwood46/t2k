using Godot;
using System;
using System.Data;

public partial class DynamicNavigationRegion : NavigationRegion3D
{
	[Export]
	public DynamicMap DynamicMap {get; private set;}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// DynamicMap = GetNode<DynamicMap>("DynamicMap");
		if(DynamicMap is null) throw new NoNullAllowedException("set Dynamic map export");
		DynamicMap.EnvironmentChanged += () => CallDeferred(MethodName.BakeNavigationMesh);
		CallDeferred(MethodName.BakeNavigationMesh);
	}

	


}
