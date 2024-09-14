using Godot;
using System;

public partial class NavCharater : CharacterBody3D
{
    [Export]
    public float Speed = 5.0f;

    [Export]
	private NavigationAgent3D nav;
	
    private Vector3 targetPos = new Vector3(0, 1, 0);
	public Vector3 MovementTarget {
		get { return nav.TargetPosition; }
		set { nav.TargetPosition = value;}
	}

	public float gravity = 0.1f;

    public override void _Ready()
    {
        base._Ready();
		nav.PathDesiredDistance = 1f;
        nav.TargetDesiredDistance = 0.5f;
		Callable.From(ActorSetup).CallDeferred();
    }

    public override void _PhysicsProcess(double delta)
	{
		if (nav.IsNavigationFinished()) return;
        Vector3 currentAgentPosition = GlobalPosition;
        Vector3 nextPathPosition = nav.GetNextPathPosition();
        Velocity = currentAgentPosition.DirectionTo(nextPathPosition) * Speed;
        MoveAndSlide();
	}

	 private async void ActorSetup()
    {
        // Wait for the first physics frame so the NavigationServer can sync.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        // Now that the navigation map is no longer empty, set the movement target.
        MovementTarget = GlobalPosition;
    }
}
