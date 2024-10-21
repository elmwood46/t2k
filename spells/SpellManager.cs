using Godot;
using System;

public partial class SpellManager : Node3D
{
	[Export]
	PlayerCharacter player;

	[Export]
	CameraController cameraController;

	SpellChainComponent spellChainHead;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		spellChainHead = new StraightShot();
		spellChainHead.Next = new StraightShot();
	}

    public override void _Input(InputEvent @event)
    {
		// if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
	    // {
	        // Vector3 from = GetParent().GetNode<CameraController>("../CameraController").Camera.ProjectRayOrigin(eventMouseButton.Position);
	        // Vector3 to = from + GetParent().GetNode<CameraController>("../CameraController").Camera.ProjectRayNormal(eventMouseButton.Position) * 4000f;
			// PhysicsRayQueryParameters3D r = PhysicsRayQueryParameters3D.Create(from, to);
	    	// Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(r);
			// Boolean noResult = res.Count <= 0; 
			// if(noResult) return;
			// Node node = (Node) res["collider"];
			// Vector3 p = (Vector3) res["position"];

		// }


		if(@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left){
			Vector3 from = cameraController.Camera.ProjectRayOrigin(eventMouseButton.Position);
	        Vector3 to = from + cameraController.Camera.ProjectRayNormal(eventMouseButton.Position) * 4000f;
			PhysicsRayQueryParameters3D r = PhysicsRayQueryParameters3D.Create(from, to);
	    	Godot.Collections.Dictionary res = GetWorld3D().DirectSpaceState.IntersectRay(r);
			Boolean noResult = res.Count <= 0; 
			if(noResult) return;
			Node node = (Node) res["collider"];
			Vector3 p = (Vector3) res["position"];

			CastPropertys c = new CastPropertys(this, player.Position, p - player.Position);
			spellChainHead.Invoke(c);
		}
    }
}
