using Godot;
using System;

public partial class CameraGimbal : Node3D
{

	// Vector3 cameraPos = new Vector3(8, 8, 8);
	// const float cameraRotation = 45;
	public Camera3D Camera {get; private set;}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Camera = GetNode<Camera3D>("Camera3D");
		

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

    public override void _Input(InputEvent @event)
    {
        
		if(@event.IsActionPressed("rotateCameraLeft")){
			RotateY(-45);
		}

		if(@event.IsActionPressed("rotateCameraRight")){
			RotateY(45);
		}
    }
}
