using Godot;
using System;

public partial class GameEnd : Control
{
	[Export]
	public Button RestartBtn {get; private set;}

	[Export]
	public Button MainMenuBtn {get; private set;}

	[Export]
	public Button QuitBtn {get; private set;}

	[Export]
	public Label LevelLabel {get; private set;}

}