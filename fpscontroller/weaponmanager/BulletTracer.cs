using Godot;

public partial class BulletTracer : Node3D
{
    public Vector3 TargetPos { get; set; } = new Vector3(0, 0, 0);

    public float Speed { get; set; } = 75.0f;

    public float TracerLength { get; set; } = 1.0f;

    private const int MaxLifetimeMs = 5000;

    private readonly ulong SpawnTime;

    public BulletTracer()
    {
        SpawnTime = Time.GetTicksMsec();
    }

    public override void _Process(double delta)
    {
        var diff = TargetPos - GlobalPosition;
        var add = diff.Normalized() * Speed * (float)delta;
        add = add.LimitLength(diff.Length());
        GlobalPosition += add;

        if ((TargetPos - GlobalPosition).Length() <= TracerLength || 
            Time.GetTicksMsec() - SpawnTime >= MaxLifetimeMs)
        {
            QueueFree();
        }
    }
}
