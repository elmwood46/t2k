using Godot;
using System.Collections.Generic;
using System.Linq.Expressions;

public enum TurnState
{
	SetupCombat,
	PlayerTurn,
	EnemyTurn,
	GotoNextTurn,
	GotoNextRound,
	EndCombat
}

public partial class TurnController : Node
{
	private BattleMenu battleMenu;

	public TurnState State { get; private set; } = TurnState.SetupCombat;

	// queue of actors 
	private List<Pawn> allActors = new();

	// used to keep track of the pawn in initiative
	private int turn = -1;
	private int round = 0;
	private Pawn _currentactor;
	private int actionPoints = 2; // action points

	// sort actors based on Mov stat
	public void UpdateInitiativeOrder()
	{
		// Sort the actors list based on the MOV property
		allActors.Sort((a, b) => a.Mov.CompareTo(b.Mov));
	}

	public void AddCombatant(Pawn p) {
		allActors.Add(p);
	}

	public override void _Ready()
	{
		// Load and instance the BattleMenu
		// var battleMenuScene = GD.Load<PackedScene>("res://ui/BattleMenu.tscn").Instantiate();
		battleMenu = GD.Load<PackedScene>("res://ui/BattleMenu.tscn").Instantiate<BattleMenu>();
		_currentactor = allActors[0];

		// Add the BattleMenu to the scene tree (under a container or parent node)
		AddChild(battleMenu);

		// Subscribe to the BattleMenuPressed event
		battleMenu.BattleMenuPressed += HandleBattleMenuPressed;
		battleMenu.Hide();
	}

	// process - state machine for running combat
	public override void _Process(double delta)
	{
		switch(this.State) {
			case TurnState.SetupCombat: // this state runs once at the start of combat
				turn = -1;
				UpdateInitiativeOrder();
				GD.Print("Setting up combat!");
				State=TurnState.GotoNextTurn;
				break;
			case TurnState.GotoNextTurn:
				turn++;
				if (turn >= allActors.Count) State=TurnState.GotoNextRound;// check for end of round
				else {
					GD.Print($"Moving to turn: {turn}");
					_currentactor = allActors[turn];
					GD.Print($"Current actor's turn: {_currentactor}");
					if (IsEnemy(_currentactor)) { // setup enemy turn
						GD.Print($"Enemy Turn.");
						State=TurnState.EnemyTurn;
					} else { 
						GD.Print($"Player Turn.");
						actionPoints = 2; // setup player turn
						State=TurnState.PlayerTurn;
						battleMenu.Show(); // show the battle menu
					}
				}
				break;
			case TurnState.EnemyTurn: // enemy turn logic here
					if (true) { // check for state exit
						State = TurnState.GotoNextTurn;
					}
				break;
			case TurnState.PlayerTurn:
				if (actionPoints <= 0) { // out of action points, next state
					battleMenu.Hide();
					State = TurnState.GotoNextTurn;
				}
				break;
			case TurnState.GotoNextRound: // do stuff at end of a round
				turn = 0;
				round++;
				GD.Print($"Round finished. Moving to round: {round}.");
				State = TurnState.GotoNextTurn;
				break;
			case TurnState.EndCombat: // currently no way to reach this
				break;
			default:
				GD.PrintErr("Error in TurnController.cs : switched to an undefined state.");
				throw new System.Exception();
		}
	}

	// Unsubscribe from the event when the node is destroyed
	public override void _ExitTree()
	{
		battleMenu.BattleMenuPressed -= HandleBattleMenuPressed;
	}

	private void HandleBattleMenuPressed(BattleMenuAction action)
	{
		GD.Print($"Action received in TurnController: {action}");

		switch (action)
		{
			case BattleMenuAction.Move:
				// Handle move action
				GD.Print("Handling move action...");
				break;
			case BattleMenuAction.Item:
				// Handle item action
				GD.Print("Handling item action...");
				break;
			case BattleMenuAction.Attack:
				// Handle attack action
				GD.Print("Handling attack action...");
				break;
			case BattleMenuAction.Skip:
				// Handle skip action
				GD.Print("Handling skip turn...");
				actionPoints = 0;
				break;
			default:
				GD.Print("Unknown action received.");
				break;
		}
		actionPoints -= 1;
		GD.Print($"Action points remaining: {actionPoints}");
	}

	// check if a pawn is an enemy of the Wizards faction
	public bool IsEnemy(Pawn p) {
		return p.IsHostile;
	}
}
