using Godot;
using System;

[Tool]
public partial class PlayerCharacter : Pawn
{
    private NavigationAgent3D nav;

    private Vector3 targetPos = new(0, 1, 0);

    public Vector3 MovementTarget
    {
        get
        {
            if (nav == null) return targetPos;  // Return targetPos if nav is null
            return nav.TargetPosition;
        }

        set
        {
            if (nav != null) nav.TargetPosition = value;  // Set nav.TargetPosition only if nav is not null
            else
            {
                GD.PrintErr("nav is null, cannot set MovementTarget.");
                targetPos = value;  // Assign to targetPos or handle as needed
            }
        }
    }

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    public override void _Ready()
    {
        UpdateAllStats();
        nav = GetNodeOrNull<NavigationAgent3D>("%NavigationAgent3D");
        if (nav == null)
        {
            throw new Exception("NavigationAgent3D not found.");
        }
        GD.Print("pawn title: ", Title, " children: ", GetChildren());
        Callable.From(ActorSetup).CallDeferred();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (nav == null) return;

        if (nav.IsNavigationFinished()) return;

        Vector3 currentAgentPosition = GlobalPosition;
        Vector3 nextPathPosition = nav.GetNextPathPosition();

        Velocity = currentAgentPosition.DirectionTo(nextPathPosition) * Speed;

        MoveAndSlide();
    }

    private async void ActorSetup()
    {
        if (nav.TargetPosition.Equals(null))
            nav.TargetPosition = targetPos;
        nav.PathDesiredDistance = 1f;
        nav.TargetDesiredDistance = 0.5f;

        // Wait for the first physics frame so the NavigationServer can sync.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        // Now that the navigation map is no longer empty, set the movement target.
        MovementTarget = GlobalPosition;
    }
}
