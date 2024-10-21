using Godot;
using System;

public partial class SpellManager : Node
{
	[Export]
	PlayerCharacter player;

	SpellChainComponent spellChainHead;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		spellChainHead = new StraightShot();
	}

    public override void _Input(InputEvent @event)
    {
		if(@event.IsAction("InvokeSpell")){
			CastPropertys c = new CastPropertys(this, player.Position, new Vector3(1, 0, 0));
			spellChainHead.Invoke(c);
		}
    }
}
