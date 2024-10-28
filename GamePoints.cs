using Godot;
using System;
public partial class GamePoints : Node
{
    private static GamePoints _instance;
    public static GamePoints Instance => _instance;

	private int _level = 1;
	private int _points = 0;
    
	public static int Points {
   		get => Instance._points; 
        private set => Instance._points = value; 
    }

    public static int Level { 
        get => Instance._level; 
        private set => Instance._level = value; 
    }
     
    public const int MaxPoints = 100; // Example value

    [Signal]
    public delegate void UpdatedEventHandler(int points, int level);


    private GamePoints() { }

    public override void _Ready()
    {
        if (_instance == null)
        {
            _instance = this;
            GD.Print("GamePoints singleton instance created.");
        }
        else
        {
            GD.PrintErr("GamePoints singleton instance already exists!");
        }
    }

    private void resetPoints()
    {
        Points = 0;
        Level = 0;
        EmitUpdatedSignal();

		Updated += (int l, int b) => {GD.Print("sdakjn");};
    }

    private void updatePoints(int value)
    {
        Points += value;
        if (Points >= MaxPoints)
        {
            Points = 0;
            Level++;
        }
        EmitUpdatedSignal();
    }

    private void EmitUpdatedSignal()
    {
        EmitSignal(nameof(Updated), Points, Level);
    }

    // Static methods to call instance methods
    public static void ResetPoints()
    {
        _instance?.resetPoints();
    }

    public static void UpdatePoints(int value)
    {
        _instance?.updatePoints(value);
    }
}