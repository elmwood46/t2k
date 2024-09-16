using Godot;
using System;
using System.Collections.Generic;

public partial class DynamicMap : GridMap
{

    private const int MAX_LENTH = 30;
	private const int INIT_BLOCK_HEALTH = 100;
	private const int CLEAR_CELL = -1;
	private int cubeIndex;
	private int rampIndex;

	private Dictionary<Vector3I, int> tileHealth;

	[Signal]
	public delegate void EnvironmentChangedEventHandler();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
		cubeIndex = MeshLibrary.FindItemByName("Box");
		rampIndex = MeshLibrary.FindItemByName("Ramp");
		tileHealth = new Dictionary<Vector3I, int>();
		GenerateMap();
	}

	/* generates gridmap enviroment */ 
	public void GenerateMap(){
		Clear();
		tileHealth.Clear();
		GenerateBasicFloor(); 
		EmitSignal(nameof(EnvironmentChanged));
	}

	/* testing method creates 30 * 30 floor */
	private void GenerateBasicFloor(){
		for(int x = 0; x < MAX_LENTH; x++){
			for(int z = 0; z < MAX_LENTH; z++){
				int offSet = MAX_LENTH / 2;
				Vector3I position = new Vector3I(x - offSet, 0, z - offSet); 
				SetCellItem(position, cubeIndex);
			}
		}
	}

	/* 
	applys damage to tile if any at collision point and removes tile when cell health is less that zero
	collisionPos: world space collision position 
	from: collision ray origin  
	dmg: damage applyed to tile if 
	 */
	public void DamageTile(Vector3 collisionPos, Vector3 from, int dmg){
		Vector3 dir = from.DirectionTo(collisionPos) * 0.1f;
		Vector3I tileCoord = GlobalPosToGridCoord(collisionPos + dir);
		if(!tileHealth.ContainsKey(tileCoord)){
			tileHealth.Add(tileCoord, INIT_BLOCK_HEALTH);
		}
		tileHealth[tileCoord] -= dmg;
		if(tileHealth[tileCoord] <= 0){
			SetCellItem(tileCoord, CLEAR_CELL);
			tileHealth.Remove(tileCoord);
			EmitSignal(nameof(EnvironmentChanged));
		}
	}

	/* translates global position to closest grid coord */ 
	private Vector3I GlobalPosToGridCoord(Vector3 pos){
		int x = (int) MathF.Round(pos.X - 0.5f);
		int y = (int) MathF.Round(pos.Y - 0.5f);
		int z = (int) MathF.Round(pos.Z - 0.5f);
		return new Vector3I(x, y, z);
	}



	// used to test collision tile health
    // public override void _UnhandledInput(InputEvent @event)
    // {
    //     if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
	//     {
	//         Vector3 from = GetParent().GetNode<CameraGimbal>("%CameraGimbal").Camera.ProjectRayOrigin(eventMouseButton.Position);
	//         Vector3 to = from + GetParent().GetNode<CameraGimbal>("%CameraGimbal").Camera.ProjectRayNormal(eventMouseButton.Position) * 4000f;
	// 		PhysicsRayQueryParameters3D r = PhysicsRayQueryParameters3D.Create(from, to);
	//     	Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(r);
	// 		Boolean noResult = res.Count <= 0; 
	// 		if(noResult) return;
	// 		Node node = (Node) res["collider"];
	// 		if(node is GridMap c){
	// 			Vector3 p = (Vector3) res["position"];
	// 			// GD.Print("init:", p);
	// 			DamageTile(p, from, 100);
	// 			// GD.Print("");
				
	// 		}
	// 	}
    // }


}
