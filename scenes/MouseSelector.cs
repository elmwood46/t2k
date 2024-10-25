using Godot;
using System;

public partial class MouseSelector : Node
{
	public Variant? SelectedObject { get; set; }
	private Variant? _previousSelectedObject;

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion)
		{
			// Get the camera node
			Camera3D camera = GetViewport().GetCamera3D();

			// Create a ray from the mouse position
			Vector2 mousePosition = GetViewport().GetMousePosition();
			Vector3 from = camera.ProjectRayOrigin(mousePosition);
			Vector3 to = from + camera.ProjectRayNormal(mousePosition) * 1000;

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

			// Check if the ray hit this pawn
			if (result.Count > 0)
			{
				SelectedObject = result["collider"];
			}
			else
			{
				SelectedObject = null;
			}
			Highlight();
		}
	}

	private void Highlight()
	{
		//remove highlight from all objects
		RemoveHighlight();

		if (SelectedObject != null && SelectedObject is Variant variant && variant.Obj != null)
		{
			// store the selected object
			_previousSelectedObject = SelectedObject;


			if (variant.Obj is Prop p)
			{
				p.IsHighlighted = true;
			}
			// Check for a more general type if needed
			else if (variant.Obj is Node3D node3D)
			{
				DebugManager.Log($"Highlighted Node3D: {node3D.Name}");
			}
			else
			{
				//GD.Print("Highlighted object of unknown type.");
			}
		}
	}

	private void RemoveHighlight()
	{
		if (!_previousSelectedObject.Equals(SelectedObject))
		{
			if (_previousSelectedObject != null && _previousSelectedObject is Variant variant && variant.Obj != null)
			{
				if (variant.Obj is Prop p)
				{ // remove prop highlight
					p.IsHighlighted = false;
				}
			}

			// reset selected object
			_previousSelectedObject = null;
		}
	}
}