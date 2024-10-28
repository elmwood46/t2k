using Godot;
using System;

public partial class Main : Node3D
{
	private const float RayLength = 1000.0f;

	[Export] public Player Player {get; set;}
	[Export] public CameraManager CameraGimbal {get; set;}
	[Export] public Area3D MapCursor {get; set;}

	private PackedScene _explosionScene = ResourceLoader.Load("res://scenes/effects/explosion.tscn") as PackedScene;

	private Vector3? _targetPosition = null;

	[Signal]
	public delegate void GameEndEventHandler();


	public override void _Ready() {
		// Input.MouseMode = Globals.DefaultMouseMode;
		Player.Instance.Died += () => EmitSignal(nameof(GameEnd));
	}


	public override void _Input(InputEvent @event)
	{
	        Vector3 from = CameraGimbal.Camera.ProjectRayOrigin(GetViewport().GetMousePosition());
	        Vector3 to = from + CameraGimbal.Camera.ProjectRayNormal(GetViewport().GetMousePosition()) * RayLength;

			// Perform the raycast to detect if the pawn is under the mouse
			PhysicsDirectSpaceState3D spaceState = GetViewport().GetWorld3D().DirectSpaceState;

			// Set up the raycast query
			PhysicsRayQueryParameters3D query = new PhysicsRayQueryParameters3D
			{
				From = from,
				To = to,
				CollisionMask = 1 // Adjust based on your layer setup
			};

			var result = spaceState.IntersectRay(query);

			if(result.Count <= 0) {
				MapCursor.Visible = false;
				_targetPosition = null;
			} else {
				MapCursor.Visible = true;
				_targetPosition = (Vector3) result["position"];
				MapCursor.Position = (Vector3) _targetPosition;
			}
		HandleClick(@event);
	}

	// sets player target to clicked location if valid
	private void HandleClick(InputEvent @event){
	    if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
	    {
			/*
			Node node = (Node) res["collider"];
			if(node is GridMap c){
				//Player.MovementTarget = (Vector3) res["position"];
				//GD.Print(res["position"]);
			}*/

			if (_targetPosition != null) {
				Explosion explosion = _explosionScene.Instantiate() as Explosion;
				explosion.Position = (Vector3)_targetPosition;
				AddChild(explosion);
			}
		}
	}
}


