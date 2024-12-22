using System;
using System.Diagnostics;
using Godot;

public partial class DebugManager : Node3D
{ 
    public static DebugOverlay Overlay;

    public static DebugManager Instance { get; private set; }

    public event Action DebugOverlayToggled;

    private PackedScene _overlayScene = GD.Load<PackedScene>("res://menus/debug_overlay.tscn");

    public static void Log(String message)
    {
        GD.Print(message);
        Overlay.DebugLog.AppendText(message + "\n");
        Overlay.DebugLog.ScrollToLine(Overlay.DebugLog.GetLineCount());
    }

    public override void _Ready()
    {
        Overlay = _overlayScene?.Instantiate<DebugOverlay>();
        AddChild(Overlay);


        #if DEBUG
            Log("Debug build");
        #endif

        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        Overlay.Visible = false;


        // subscribe to the event
        DebugOverlayToggled += OnDebugOverlayToggled;
    }
    
   public override void _UnhandledInput(InputEvent @event)
    {
        #if DEBUG
        // Check if the input action is pressed
        if (@event.IsActionPressed("toggle_debug_overlay"))
        {
            DebugOverlayToggled?.Invoke();
        }
        if (@event.IsActionPressed("debug_restart"))
		{
			GetTree().ReloadCurrentScene();
		}
        #endif
    }

    private void OnDebugOverlayToggled()
    {
        Instance.Visible = !Instance.Visible;
        Overlay.Visible = Instance.Visible;
    }
}