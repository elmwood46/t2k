using Godot;
using System;

public partial class EnemyManager : Node3D
{
	PackedScene enemyScene;
	Timer spawnTimer;

	float spawnRange = 30;

	float startingTime = 2;
	float minTime = 0.1f;

	public override void _Ready()
	{
		base._Ready();
		enemyScene = GD.Load<PackedScene>("res://enemy/enemy.tscn");

		spawnTimer = new Timer();
		spawnTimer.WaitTime = startingTime;
		spawnTimer.Autostart = true;
		spawnTimer.OneShot = false;
		spawnTimer.Timeout += OnSpawnTimerTimeout;

		GamePoints.Instance.Updated += Updated; 
		AddChild(spawnTimer);
	}

    private void Updated(int points, int level)
    {
		spawnTimer.WaitTime = Mathf.Max(minTime, startingTime - (level * 0.1));
    }

    private void OnSpawnTimerTimeout()
	{
		var enemyInstance = enemyScene.Instantiate<Enemy>();
		AddChild(enemyInstance);
		Vector3 dir = Vector3.Forward.Rotated(Vector3.Up, Mathf.DegToRad(new Random().Next(360))) * spawnRange; 
		enemyInstance.GlobalPosition = Player.Instance.GlobalPosition + Vector3.Up * 10 + dir;
	}
}
