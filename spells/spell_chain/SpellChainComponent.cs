using Godot;
using System;

public abstract partial class SpellChainComponent : Node
{
	// string description;
	public abstract string getDescription();

	SpellChainComponent next;
	// Sprite icon; 
	public abstract void Invoke(CastPropertys cast);
}

public partial class CastPropertys{
	public Vector3 Direction {get; private set;}
	public Vector3 Origin {get; private set;}
	
	private int currentSpellDepth = 0;
	public int CurrentSpellDepth {get; set;}
	public Node SceneReference {get; private set;}
	

	public CastPropertys(Node sceneRef, Vector3 origin, Vector3 direction){
		this.SceneReference = sceneRef;
		this.Origin = origin;
		this.Direction = direction;
	}
}