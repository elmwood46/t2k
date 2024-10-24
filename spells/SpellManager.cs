using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public partial class SpellManager : Node3D
{
	public const int MAX_SPELL_LENGTH = 6;

	[Export]
	PlayerCharacter player;

	[Export]
	CameraController cameraController;

	SpellChainComponent spellChainHead {get => spellChain[0]; set => spellChain[0] = value;}

	[Export]
	SpellCraftBar spellCraftBar;

	private List<SpellChainComponent> spellChain = new List<SpellChainComponent>();
	public List<SpellChainComponent> SpellChainList {get => spellChain;}


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AddSpell(new StraightShot(new FireElement()));
		AddSpell(new SpellSplitter());
		AddSpell(new GrenadeSpell(new EarthElement()));

		spellCraftBar.SpellsSwapped += SwapSpells;
	}

	public void AddSpell(SpellChainComponent spell){
		if(spellChain.Count <= 0){
			spellChain.Add(spell);
		}else{
			int endIndex = spellChain.Count - 1;
			spellChain[endIndex].Next = spell;
			spellChain.Add(spell);
		}
	}

	public void InsertSpell(int i, SpellChainComponent spell){
		if(i >= spellChain.Count) throw new IndexOutOfRangeException();
		if(i < 0) throw new IndexOutOfRangeException();
		spell.Next = spellChain[i];
		if(i != 0){
			spellChain[i - 1].Next = spell;
		}
		spellChain.Insert(i, spell);
	}

	public void RemoveSpell(int index){
		if(index >= spellChain.Count){ throw new IndexOutOfRangeException();}
		if(index < 0) throw new IndexOutOfRangeException();
		if(index == 0){
			spellChain.RemoveAt(index);
		}else{
			SpellChainComponent removed = spellChain[index];
			SpellChainComponent rmvdPointer = spellChain[index - 1];
			if(removed.Next != null){
				rmvdPointer.Next = removed.Next;
			}
			spellChain.RemoveAt(index);
		}
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

	public void SwapSpells(int from, int to){
		if(spellChainHead == null || spellChain.Count == 0) return;
		if(from >= spellChain.Count) return;

		to = Math.Clamp(to, 0, spellChain.Count - 1);

		SpellChainComponent toComp = spellChain[to];
		SpellChainComponent fromComp = spellChain[from];
		spellChain[to] = fromComp;
		spellChain[from] = toComp;
		
		for(int i = 0; i < spellChain.Count; i++){
			if(i == spellChain.Count - 1){
				spellChain[i].Next = null;
			}else{
				spellChain[i].Next = spellChain[i + 1];
			}
		}
		spellCraftBar.UpdateBar(spellChain);
		// change to to from and change next value
		// change from to 
	}
}
