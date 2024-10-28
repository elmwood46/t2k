using Godot;
using System;

public partial class MainMenu : Control
{

	PackedScene gameScene = GD.Load<PackedScene>("res://main.tscn");

	[Export]
	public Button PlayBtn {get; private set;}
	[Export]
	public Button QuitBtn {get; private set;}

}
