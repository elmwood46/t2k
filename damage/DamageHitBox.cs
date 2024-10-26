using Godot;
using System;

public partial class DamageHitBox : StaticBody3D
{
	[Export]
	Enemy target;

	public void applyDamage(int dmg){
		ShowDamageLabel(dmg);
		target?.TakeDamage(dmg);
	}

	public void ShowDamageLabel(int dmg)
	{
		var label = new Label3D();
		label.Text = dmg.ToString();
		label.Position = this.Position;
		label.PixelSize = 0.008f;
		AddChild(label);

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(label, "position:y", label.Position.Y + 2, 1.0f);
		tween.TweenProperty(label, "modulate:a", 0, 1.0f);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}
}
