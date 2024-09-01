using Godot;
using System;

public partial class PlayerCharacter : CharacterBody3D
{
    public const float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;

    private NavigationAgent3D nav;

    private Vector3 targetPos = new Vector3(0, 1, 0);

    public Vector3 MovementTarget
    {
        get { return nav.TargetPosition; }
        set { nav.TargetPosition = value; }
    }

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        base._Ready();

        nav = GetNode<NavigationAgent3D>("%NavigationAgent3D");
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
