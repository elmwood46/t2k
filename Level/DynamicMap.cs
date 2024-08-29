using Godot;
using System;

public partial class DynamicMap : GridMap
{
    private const int MAX_LENTH = 30;
	private int cubeIndex;
	private int rampIndex;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
		cubeIndex = MeshLibrary.FindItemByName("Cube");
		rampIndex = MeshLibrary.FindItemByName("Ramp");
		GenerateMap();
	}

	/* generates gridmap enviroment */ 
	public void GenerateMap(){
		Clear();
		GenerateBasicFloor(); 
	}

	/* testing method creates 30 * 30 floor */
	private void GenerateBasicFloor(){
		for(int x = 0; x < MAX_LENTH; x++){
			for(int z = 0; z < MAX_LENTH; z++){
				int offSet = MAX_LENTH / 2;
				Vector3I position = new Vector3I(x - offSet, 1, z - offSet); 
				SetCellItem(position, cubeIndex);
			}
		}
	}
}
