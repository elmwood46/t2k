using Godot;
using System;

public partial class EnemyManager : Node3D
{
	PackedScene enemyScene;
	Timer spawnTimer;

	public override void _Ready()
	{
		base._Ready();
		enemyScene = GD.Load<PackedScene>("res://enemy/enemy.tscn");

		spawnTimer = new Timer();
		spawnTimer.WaitTime = 2f;
		spawnTimer.Autostart = true;
		spawnTimer.OneShot = false;
		spawnTimer.Timeout += OnSpawnTimerTimeout;
		AddChild(spawnTimer);
	}

	private void OnSpawnTimerTimeout()
	{
		var enemyInstance = enemyScene.Instantiate<Enemy>();
		AddChild(enemyInstance);
		enemyInstance.GlobalPosition = Player.Instance.GlobalPosition + Vector3.Up * 10;
	}
}
