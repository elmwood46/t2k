using Godot;
using System;

/*

*/
public abstract partial class SpellChainComponent : Node
{
	// string description;
	// public abstract string getDescription();
	public abstract Texture Icon {get;}
	public abstract string Description {get;}
	public SpellChainComponent Next {get; set;}
	

	/* 
	performs action described in spells discription and then call invoke on next component 
	cast propertys provide scene access and information about cast behavour 
	*/
	public abstract void Invoke(CastPropertys cast);
}

public partial class CastPropertys{
	public Vector3 Direction {get; private set;}
	public Vector3 Origin {get; set;}
	
	private int currentSpellDepth = 0;
	public int CurrentSpellDepth {get; set;}
	public Node SceneReference {get; private set;}
	

	public CastPropertys(Node sceneRef, Vector3 origin, Vector3 direction){
		this.SceneReference = sceneRef;
		this.Origin = origin;
		this.Direction = direction;
	}
}