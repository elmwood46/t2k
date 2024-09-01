using Godot;

// A class to track the mouse velocity in pixels per microseconds
public partial class MouseVelocityTracker : RefCounted
{
    private Vector2 _lastMousePosition;
    private Vector2 _mouseVelocity;
    private Viewport _viewport;
    public MouseVelocityTracker(Viewport viewport)
    {
        _viewport = viewport;
        _lastMousePosition = _viewport.GetMousePosition();
        _mouseVelocity = new Vector2(0f,0f);
    }

    // update and set mouse velocity in pixels per microseconds
    public void Update(double delta)
    {
        Vector2 currentMousePosition = _viewport.GetMousePosition();
        _mouseVelocity = (currentMousePosition - _lastMousePosition) * (float)(1.0/delta);
        _lastMousePosition = currentMousePosition;
    }

    // Public method to access the mouse velocity
    // returns the mouse velocity in pixels per microseconds
    public Vector2 GetMouseVelocity()
    {
        return _mouseVelocity;
    }
}