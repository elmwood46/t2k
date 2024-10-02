using Godot;
using System;

//enum for battle menu
    public enum BattleMenuAction
{
    Move,
    Item,
    Attack,
    Skip
}

public partial class BattleMenu : Control
{
    // Declare a single event that takes a string argument
    public event Action<BattleMenuAction> BattleMenuPressed;

    private Button moveButton;
    private Button itemButton;
    private Button attackButton;
    private Button skipButton;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        // Get the buttons by their names
        moveButton = GetNode<Button>("Panel/btnMove");
        itemButton = GetNode<Button>("Panel/btnItem");
        attackButton = GetNode<Button>("Panel/btnAttack");
        skipButton = GetNode<Button>("Panel/btnSkip");

        // Connect button signals to their respective methods
        moveButton.Pressed += () => OnButtonPressed(BattleMenuAction.Move);
        itemButton.Pressed += () => OnButtonPressed(BattleMenuAction.Item);
        attackButton.Pressed += () => OnButtonPressed(BattleMenuAction.Attack);
        skipButton.Pressed += () => OnButtonPressed(BattleMenuAction.Skip);
	}

    private void OnButtonPressed(BattleMenuAction action)
    {
        GD.Print($"{action} button pressed.");
        BattleMenuPressed?.Invoke(action); // Emit the event with the action
    }
}
