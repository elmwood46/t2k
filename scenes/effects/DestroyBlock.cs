using Godot;
using System;

public partial class DestroyBlock : GpuParticles3D
{
	public override void _Ready()
	{
		OneShot = true;
		Emitting = true;

        // Create a Timer to destroy the object after lifetime ends
        Timer _timer = new()
        {
            WaitTime = Lifetime, // Set the timer to the particle's lifetime
            OneShot = true
        };
        _timer.Connect("timeout", new Callable(this, nameof(OnTimerTimeout)));
        AddChild(_timer);
        _timer.Start();
	}

	private void OnTimerTimeout()
	{
		QueueFree();
	}
}