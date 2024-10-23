using Godot;
using System;

public partial class MenuManager : Node3D
{
    public static MenuManager Instance { get; private set; }

    public event Action PauseMenuToggled;

    public static Control PauseMenu { get; private set; }
    private PackedScene _pauseMenuScene = GD.Load<PackedScene>("res://menus/pause_menu.tscn");
    
    public override void _Ready()
    {
        Instance = this;
        Visible = false;
        PauseMenu = _pauseMenuScene?.Instantiate<Control>();
        AddChild(PauseMenu);
        PauseMenu.Visible = false;
        ProcessMode = ProcessModeEnum.Always;

        // subscribe to the event
        PauseMenuToggled += OnPauseMenuToggled;
    }

   public override void _UnhandledInput(InputEvent @event)
    {
        // Check if the input action is pressed
        if (@event.IsActionPressed("pause"))
        {
            PauseMenuToggled?.Invoke();
        }
    }

    private void OnPauseMenuToggled()
    {
        Instance.Visible = !Instance.Visible;
        GetTree().Paused = Instance.Visible;
        PauseMenu.Visible = Instance.Visible;
        Input.MouseMode = Instance.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }
}
