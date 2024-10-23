using Godot;
using System;
using System.Collections.Generic;

public partial class SpellManager : Node3D
{
	public const int MAX_SPELL_LENGTH = 6;

	[Export]
	PlayerCharacter player;

	[Export]
	CameraController cameraController;
	SpellChainComponent spellChainHead;

	[Export]
	SpellCraftBar spellCraftBar;

	private List<SpellChainComponent> spellChain = new List<SpellChainComponent>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// spellChainHead = new GrenadeSpell(new EarthElement());

		// spellChainHead = new StraightShot(new EarthElement());
		// spellChainHead.Next = new GrenadeSpell(new EarthElement());

		spellChainHead = new StraightShot(new FireElement());
		spellChainHead.Next = new SpellSplitter();
		spellChainHead.Next.Next = new SpellSplitter();
		spellChainHead.Next.Next.Next = new GrenadeSpell(new EarthElement());
		// spellChainHead.Next.Next.Next.Next = new StraightShot(new EarthElement()); 

		spellCraftBar.SpellsSwapped += (int from, int to) => {
			GD.Print(from ," -> " ,to);
		};
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

			Vector3 dir = p - player.Position;
			dir = new Vector3(dir.X, 0, dir.Z);

			CastPropertys c = new CastPropertys(this, player.Position, dir);
			spellChainHead.Invoke(c);
		}
    }
}
