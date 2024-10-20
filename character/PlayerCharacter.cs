using Godot;
using System;

[Tool]
public partial class PlayerCharacter : Pawn
{
    new private NavigationAgent3D Nav;

    // Get the gravity from the project settings to be synced with RigidBody nodes.
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    [Export] new public float Speed = 5.0f; // Movement speed
    [Export] public float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
    [Export] public float JumpStrength = 1.0f; // Jump force

    private Vector3 _velocity = Vector3.Zero; // The player's velocity
    private bool _isJumping = false; // To track if the player is jumping
    public bool Dead { get; set; } = false;


    public Vector3 MovementTarget
    {
        get
        {
            if (Nav == null) return GlobalPosition;
            return Nav.TargetPosition;
        }

        set
        {
            if (Nav != null) Nav.TargetPosition = value;  // Set nav.TargetPosition only if nav is not null
            else
            {
                GD.PrintErr("nav is null, cannot set MovementTarget.");
            }
        }
    }


    // dont QueueFree when player character dies
    public override void Die() {
        Visible = false;
        Dead = true;
        GD.Print("PlayerCharacter died.");
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return; //don't run in editor
        base._Ready();
        Nav = GetNodeOrNull<NavigationAgent3D>("%NavigationAgent3D");
        if (Nav == null) throw new Exception("Ran PlayerCharacter _Ready(): %NavigationAgent3D not found.");
        Callable.From(ActorSetup).CallDeferred();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return; //don't run in editor

        // outline this sprite when it is selected
        UpdateOutline();

        if (!Dead) HandleMovement(delta);

        /*

        // do navigation
        if (nav == null) return;

        if (nav.IsNavigationFinished()) return;

        Vector3 currentAgentPosition = GlobalPosition;
        Vector3 nextPathPosition = nav.GetNextPathPosition();

        Velocity = currentAgentPosition.DirectionTo(nextPathPosition) * Speed;

        MoveAndSlide();*/
    }

    private async void ActorSetup()
    {
        Nav.PathDesiredDistance = 1f;
        Nav.TargetDesiredDistance = 0.5f;

        // Wait for the first physics frame so the NavigationServer can sync.
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        // Now that the navigation map is no longer empty, set the movement target.
        MovementTarget = GlobalPosition;
    }

    
    private void HandleMovement(double delta)
    {


        // Apply gravity
        if (!IsOnFloor())
        {
            _velocity.Y -= Gravity * (float)delta;

            if (Position.Y < -100f) Die(); // fall out of map
        }
        else
        {
            _velocity.Y = 0;

            // Jumping logic
            if (Input.IsActionJustPressed("jump") && !_isJumping && StunTimer.IsStopped())
            {
                _velocity.Y = JumpStrength;
                _isJumping = true;
            }
        }

        if (StunTimer.IsStopped()) { //update player movement velocity
            // Capture input for movement (WASD or arrow keys)
            Vector2 _ipt = Input.GetVector("moveLeft","moveRight","moveUp","moveDown");

            // Rotate input dir relateive to camera and Normalize the direction to prevent faster diagonal movement
            Vector3 direction = new Vector3(_ipt.X, 0f, _ipt.Y)
                .Rotated(Vector3.Up, GetParent().GetNode<CameraController>("CameraController").Rotation.Y)
                .Normalized();
            // Move the character
            _velocity.X = direction.X * Speed;
            _velocity.Z = direction.Z * Speed;

            Velocity = _velocity;
        } else {
            Velocity = new Vector3(Velocity.X,Velocity.Y-Gravity * (float)delta,Velocity.Z);
            if (IsOnFloor()) Velocity *= 0.9f; // apply friction
            else Velocity *=0.99f; // apply air resistance
        }

        MoveAndSlide(); 

        // Reset jump state when the player lands
        if (IsOnFloor())
        {
            _isJumping = false;
        }

    }

}
