using Godot;
using System;

public partial class HealthLabel : Label
{
	[Export]
	Player player;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Text = "Health " + player.Health;
		player.Damaged += () => Text = "Health " + player.Health;
	}
}
