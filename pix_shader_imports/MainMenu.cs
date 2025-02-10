using Godot;
using System;

public partial class MainMenu : Node3D
{
	[Export] public AudioStreamPlayer2D music;
	[Export] public ColorRect fade;
	[Export] public MeshInstance3D rotatingCube;
	[Export] public Label introText;
	[Export] public AnimationPlayer animPlayer;

	[Export] public Camera3D camera;

	private float timeBeforeFade = 1.0f;
	private float fadeTime = 3.0f;
	private Timer timer;
	private bool isFadedIn = false;

	public override void _Ready() {
		animPlayer.Play("skip_intro");
		fade.Color = new Color(0,0,0,1);
		timer = new Timer() {
			WaitTime = timeBeforeFade,
			OneShot = true
		};
		timer.Timeout += _on_Timer_timeout;
		AddChild(timer);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		rotatingCube.RotateY(Mathf.Pi * 0.5f * (float)delta);

		if (isFadedIn) {
			return;
		}
		if (!animPlayer.IsPlaying() && timer.IsStopped()) {
			music.Play();
			timer.Start();
		}
	}

	private void _on_Timer_timeout() {
		isFadedIn = true;
		Tween t = fade.CreateTween();
		t.TweenProperty(fade, "color", new Color(0,0,0,0), fadeTime);
		Tween t2 = introText.CreateTween();
		t2.TweenProperty(introText, "/modulate", new Color(255,255,255,0), fadeTime);
		Tween t3 = camera.CreateTween();
		t3.SetTrans(Tween.TransitionType.Sine);
		t3.SetEase(Tween.EaseType.Out);
		t3.TweenProperty(camera, "/rotation_degrees", new Vector3(-30.0f,45.0f,0.0f), 5.0f);
		Tween t4 = camera.CreateTween();
		t4.SetTrans(Tween.TransitionType.Sine);
		t4.SetEase(Tween.EaseType.Out);
		t4.TweenProperty(camera, "/position", new Vector3(3.0f,3.0f,3.0f), 5.0f);
	}

	public void SetLabelTransparency(float alpha) {
		Color c = introText.Modulate;
		c[3] = alpha;
		introText.Modulate = c;
	}
}