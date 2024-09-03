using Godot;

[Tool]  
public partial class BasicPawn : Pawn
{
    public override void _Ready()
    {
        base._Ready(); // Calls the _Ready() method in Pawn
        GD.Print("BasicPawn _Ready called");
        // Additional initialization for BasicPawn
    }

	public override void _PhysicsProcess(double delta)
	{

	}
}
