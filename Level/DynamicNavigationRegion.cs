using Godot;
using System;

public partial class DynamicNavigationRegion : NavigationRegion3D
{
	public DynamicMap DynamicMap {get; private set;}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		DynamicMap = GetNode<DynamicMap>("GridMap");
		
		CallDeferred(MethodName.BakeNavigationMesh);
		DynamicMap.EnvironmentChanged += () => CallDeferred(MethodName.BakeNavigationMesh);
	}

	


}
