using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DynamicMap : GridMap
{
	PackedScene destroyed_block = GD.Load<PackedScene>("res://shaders/DestroyedBlock.tscn"); 

    private const int MAX_LENTH = 30;
	private const int INIT_BLOCK_HEALTH = 100;
	private const int CLEAR_CELL = -1;
	private int cubeIndex;
	private int rampIndex;
	private Dictionary<Vector3I, int> tilesHealth;
	private Dictionary<Vector3I, float> tilesYOffset;
	private Dictionary<Timer, GpuParticles3D> removeParticles = new Dictionary<Timer, GpuParticles3D>();
	

	[Signal]
	public delegate void EnvironmentChangedEventHandler();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
		cubeIndex = MeshLibrary.FindItemByName("Grass");
		rampIndex = MeshLibrary.FindItemByName("Stone");
		tilesHealth = new Dictionary<Vector3I, int>();
		GenerateMap();
	}

	/* generates gridmap enviroment */ 
	public void GenerateMap(){
		Clear();
		tilesHealth.Clear();
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
		Vector3I tileCoord = LocalToMap(collisionPos + dir);
		if(!tilesHealth.ContainsKey(tileCoord)){
			tilesHealth.Add(tileCoord, INIT_BLOCK_HEALTH);
		}
		tilesHealth[tileCoord] -= dmg;
		if(tilesHealth[tileCoord] <= 0){
			// destroy block

			// animate with a timer
			GpuParticles3D break_anim = (GpuParticles3D)destroyed_block.Instantiate();
			break_anim.Position =  MapToLocal(tileCoord) + new Vector3(0.0f, 0.2f, 0.0f); // add some yoffset
			break_anim.Emitting = true;	
			break_anim.OneShot = true;
			Timer timer = new() {
				WaitTime = break_anim.Lifetime, // make*2 to ensure particles finish
				Autostart = true,
				OneShot = true
			};
			timer.Connect("timeout", new Callable(this, nameof(OnParticlesFinished)));
			
			// add timer and gpu anim to the scene tree
			AddChild(timer);
			AddChild(break_anim);
			removeParticles.Add(timer,break_anim);

			// remove block
			SetCellItem(tileCoord, CLEAR_CELL);
			tilesHealth.Remove(tileCoord);
			EmitSignal(nameof(EnvironmentChanged));
		}
	}

//	used to test collision tile health
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
	    {
	        Vector3 from = GetParent().GetNode<CameraController>("../CameraController").Camera.ProjectRayOrigin(eventMouseButton.Position);
	        Vector3 to = from + GetParent().GetNode<CameraController>("../CameraController").Camera.ProjectRayNormal(eventMouseButton.Position) * 4000f;
			PhysicsRayQueryParameters3D r = PhysicsRayQueryParameters3D.Create(from, to);
	    	Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(r);
			Boolean noResult = res.Count <= 0; 
			if(noResult) return;
			Node node = (Node) res["collider"];
			if(node is GridMap c){
				Vector3 p = (Vector3) res["position"];
				// GD.Print("init:", p);
				DamageTile(p, from, 100);
				// GD.Print("");
				
			}
		}
    }


	// remove particle emitter from the scene
	private void OnParticlesFinished()
    {
		removeParticles = removeParticles.Where(kvp => kvp.Key.TimeLeft <= 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);  // Rebuild the dictionary
	}
}
