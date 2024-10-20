using Godot;
using System;

public partial class Level : Node3D
{
	private const float RayLength = 1000.0f;

	PlayerCharacter player;
	CameraController cameraGimbal;

	PackedScene explosionScene = ResourceLoader.Load("res://effects/Explosion.tscn") as PackedScene;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// player = GetNode<PlayerCharacter>("PlayerCharacter");
		cameraGimbal = GetNode<CameraController>("CameraController");
		player = GetNode<PlayerCharacter>("PlayerCharacter");
	}

	public override void _Input(InputEvent @event)
	{
		HandleClick(@event);
	}

	// sets player target to clicked location if valid
	private void HandleClick(InputEvent @event){
	    if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
	    {
	        Vector3 from = cameraGimbal.Camera.ProjectRayOrigin(eventMouseButton.Position);
	        Vector3 to = from + cameraGimbal.Camera.ProjectRayNormal(eventMouseButton.Position) * RayLength;
			PhysicsRayQueryParameters3D r = PhysicsRayQueryParameters3D.Create(from, to);
	    	Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(r);

			Boolean noResult = res.Count <= 0; 
			if(noResult) return;
			Vector3 pos = (Vector3) res["position"]; 
			Node node = (Node) res["collider"];
			if(node is GridMap c){
				player.MovementTarget = (Vector3) res["position"];
				//GD.Print(res["position"]);
			}
			Explosion explosion = explosionScene.Instantiate() as Explosion;
			explosion.Position = pos;
			GD.Print("Explosion instantiated at: ", pos);
			AddChild(explosion);
		}
	}


}


