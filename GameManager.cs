using Godot;
using System;

public partial class GameManager : Node3D
{
	PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");

	[Export]
	MainMenu mainMenu;

	Main main;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		mainMenu.PlayBtn.Pressed += Start;
		mainMenu.QuitBtn.Pressed += Quit;

		ResetGame();
        Input.MouseMode = Input.MouseModeEnum.Visible;
	}

    private void Start()
    {
		GD.Print("start");
        mainMenu.Visible = false;
		GetTree().Paused = false;
        Input.MouseMode = Globals.DefaultMouseMode;
    }

    private void Quit()
    {
		GetTree().Quit();
    }

	private void GameEnd(){
        Input.MouseMode = Input.MouseModeEnum.Visible;
		mainMenu.Visible = true;
		GetTree().Paused = true;
		ResetGame();
	}

	private void ResetGame(){
		if(main != null){
			RemoveChild(main);
		}
		main = packedMain.Instantiate<Main>();
		main.GameEnd += GameEnd;
		AddChild(main);
	}

}
