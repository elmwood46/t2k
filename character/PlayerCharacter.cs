using Godot;
using System;

[Tool]
public partial class PlayerCharacter : Pawn
{
    private NavigationAgent3D nav;

    public Vector3 MovementTarget
    {
        get
        {
            if (nav == null) return GlobalPosition;
            return nav.TargetPosition;
        }

        set
        {
            if (nav != null) nav.TargetPosition = value;  // Set nav.TargetPosition only if nav is not null
            else
            {
                GD.PrintErr("nav is null, cannot set MovementTarget.");
            }
        }
    }

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        base._Ready();
        nav = GetNodeOrNull<NavigationAgent3D>("%NavigationAgent3D");
        if (nav == null) throw new Exception("Ran PlayerCharacter _Ready(): %NavigationAgent3D not found.");
        Callable.From(ActorSetup).CallDeferred();
    }

    public override void _PhysicsProcess(double delta)
    {
        // outline this sprite when it is selected
        UpdateOutline();

        // do navigation
        if (nav == null) return;

        if (nav.IsNavigationFinished()) return;

        Vector3 currentAgentPosition = GlobalPosition;
        Vector3 nextPathPosition = nav.GetNextPathPosition();

        Velocity = currentAgentPosition.DirectionTo(nextPathPosition) * Speed;

        MoveAndSlide();
    }

    private async void ActorSetup()
    {
        nav.PathDesiredDistance = 1f;
        nav.TargetDesiredDistance = 0.5f;

        // Wait for the first physics frame so the NavigationServer can sync.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        // Now that the navigation map is no longer empty, set the movement target.
        MovementTarget = GlobalPosition;
    }
}
