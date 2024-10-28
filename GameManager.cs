using Godot;
using System;

public partial class GameManager : Node3D
{
	PackedScene packedMain = GD.Load<PackedScene>("res://main.tscn");

	[Export]
	MainMenu mainMenu;

	[Export]
	GameEnd gameEndMenu;

	Main main;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		mainMenu.PlayBtn.Pressed += Start;
		mainMenu.QuitBtn.Pressed += Quit;

		gameEndMenu.RestartBtn.Pressed += Start;
		gameEndMenu.MainMenuBtn.Pressed += GoToMainMenu;
		gameEndMenu.QuitBtn.Pressed += Quit;
		gameEndMenu.Visible = false;

		ResetGame();
        Input.MouseMode = Input.MouseModeEnum.Visible;
	}

    private void GoToMainMenu()
    {
		mainMenu.Visible = true;
		gameEndMenu.Visible = false;
    }

    private void Start()
    {
		GamePoints.ResetPoints();
		GD.Print("start");
        mainMenu.Visible = false;
		gameEndMenu.Visible = false;
		GetTree().Paused = false;
        Input.MouseMode = Globals.DefaultMouseMode;
    }

    private void Quit()
    {
		GetTree().Quit();
    }

	private void GameEnd(){
        Input.MouseMode = Input.MouseModeEnum.Visible;
		gameEndMenu.Visible = true;
		gameEndMenu.LevelLabel.Text = "Level Reached: " + GamePoints.Level;
		GetTree().Paused = true;
		ResetGame();
	}

	private void ResetGame(){
		GamePoints.ResetPoints();
		if(main != null){
			RemoveChild(main);
		}
		main = packedMain.Instantiate<Main>();
		main.GameEnd += GameEnd;
		AddChild(main);
	}

}
