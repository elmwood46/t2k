using Godot;
using System;

public partial class WeaponManager : Node3D
{
	[Export] public RayCast3D BulletRaycast { get; set; }

	[Export] public WeaponResource CurrentWeapon {
		get => _currentWeapon;
		set {
			if (_currentWeapon != null) {
				_currentWeapon.IsEquipped = false;
			}
			_currentWeapon = value;
			if (IsInsideTree()) UpdateWeaponModel();
		}
	}
	WeaponResource _currentWeapon = null;

	[Export] public Node3D ViewModelContainer;
	[Export] public Node3D WorldModelContainer;

	[Export] public bool AllowShooting { get; set; } = true;

	private Node3D _current_weapon_view_model = null;
	private Node3D _current_weapon_muzzle = null;
	private Node3D _current_weapon_world_model = null;

	private AudioStreamPlayer3D _audioStreamPlayer;
	private GpuParticles3D _muzzleFlashEffect;

	private float _recoilHeat = 0f;
	private float _recoilHeatRecoverSpeed = 10.0f;

	private string _lastPlayedAnim = "";
	private Callable? _currentAnimFinishedCallback;
	private Callable? _currentAnimCancelledCallback;

	public static WeaponManager Instance { get; private set; }

	private void UpdateWeaponModel() {
		GD.Print("Updating weapon model");
		if (CurrentWeapon == null) return;
		GD.Print("Current weapon: " + CurrentWeapon);
		if (ViewModelContainer != null && CurrentWeapon.ViewModel != null) {
			GD.Print("View model container: " + ViewModelContainer.Name);
			_current_weapon_view_model = (Node3D)CurrentWeapon.ViewModel.Instantiate();
			_current_weapon_muzzle = FindNode3DRecursive(_current_weapon_view_model, "Muzzle");
			ViewModelContainer.AddChild(_current_weapon_view_model);
			_current_weapon_view_model.Position = CurrentWeapon.ViewModelPos;
			_current_weapon_view_model.Rotation = CurrentWeapon.ViewModelRot;
			_current_weapon_view_model.Scale = CurrentWeapon.ViewModelScale;
			var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
			animPlayer?.Connect("current_animation_changed", new Callable(this,nameof(CurrentAnimChanged)));
			ShaderUtils.ApplyClipAndFovShaderToViewModel(_current_weapon_view_model);
			_current_weapon_world_model.Position = CurrentWeapon.WorldModelPos;
			_current_weapon_world_model.Rotation = CurrentWeapon.WorldModelRot;
			_current_weapon_world_model.Scale = CurrentWeapon.WorldModelScale;
		}
		if (WorldModelContainer != null && CurrentWeapon.WorldModel != null) {
			GD.Print("World model container: " + WorldModelContainer.Name);
			_current_weapon_world_model = (Node3D)CurrentWeapon.WorldModel.Instantiate();
			WorldModelContainer.AddChild(_current_weapon_world_model);
		}
		GD.Print("Weapon model updated");
		CurrentWeapon.IsEquipped = true;
		GD.Print("Weapon equipped");
		if (Player.Instance.HasMethod("UpdateViewAndWorldModelMasks")) {
			Player.Instance.UpdateViewAndWorldModelMasks();
		}
		GD.Print("View and world model masks updated");
	}

    private static Node3D FindNode3DRecursive(Node node, string nameToFind)
    {
		if (node.Name.Equals(nameToFind)) { return (Node3D)node;}
        foreach (Node child in node.GetChildren())
        {
            Node3D result = FindNode3DRecursive(child, nameToFind);
			if (result != null && result.Name.Equals(nameToFind)) { return result;}
        }
		return null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
		if (CurrentWeapon != null && IsInsideTree()) {
			if (@event.IsActionPressed("Shoot") && AllowShooting) {
				CurrentWeapon.TriggerDown = true;
			} else if (@event.IsActionReleased("Shoot")) {
				CurrentWeapon.TriggerDown = false;
			}
			if (@event.IsActionPressed("Reload")) {
				CurrentWeapon.ReloadPressed();
			}
		}
    }

    public override void _Ready()
    {
        Instance = this;
		_audioStreamPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");
		_muzzleFlashEffect = GetNode<GpuParticles3D>("MuzzleFlash");
        UpdateWeaponModel();
    }

	public void ShowMuzzleFlash() {
		_muzzleFlashEffect.Emitting = true;
	}

	public void MakeBulletTrail(Vector3 target) {
		if (_current_weapon_muzzle == null) return;
		var muzzle_pos = _current_weapon_muzzle.GlobalPosition;
		var bullet_dir = (target - muzzle_pos).Normalized();
		var start_pos = muzzle_pos + bullet_dir * 0.25f;
		if ((target - start_pos).Length() > 3.0f) {
			var bullet_tracer = (BulletTracer)GD.Load<PackedScene>("res://fpscontroller/weaponmanager/bullet_tracer.tscn").Instantiate();
			Player.Instance.AddSibling(bullet_tracer);
			bullet_tracer.GlobalPosition = start_pos;
			bullet_tracer.TargetPos = target;
			bullet_tracer.LookAt(target, Vector3.Up);
		}
	}

	public static Vector2 GetCurrentRecoil() {
		return Player.Instance.GetCurrentRecoil();
	}

	public void ApplyRecoil() {
		Vector2 sprayRecoil = Vector2.Zero;
		if (CurrentWeapon.SprayPattern != null) {
			sprayRecoil = CurrentWeapon.SprayPattern.GetPointPosition((int)_recoilHeat % CurrentWeapon.SprayPattern.PointCount)* 0.0005f;
		}
		var randomRecoil = new Vector2((float)GD.RandRange(-1.0f, 1.0f), (float)GD.RandRange(-1.0f, 1.0f)) * 0.01f;
		var recoil = sprayRecoil;// + randomRecoil; // negative because the recoil pattern is set up in godot 2d space
		Player.Instance.AddRecoil(-recoil.Y,-recoil.X); // Y component is pitch, X component is yaw
		_recoilHeat += 1.0f;
	}

	public override void _Process(double delta) // align position of muzzle flash
	{
		if (_current_weapon_muzzle != null) 
			_muzzleFlashEffect.GlobalPosition = _current_weapon_muzzle.GlobalPosition;

		_recoilHeat = Mathf.Max(0f, _recoilHeat - _recoilHeatRecoverSpeed * (float)delta);

		CurrentWeapon?.OnProcess((float) delta);
	}

	public void PlaySound(AudioStream sound) {
		if (_audioStreamPlayer.Stream != sound) _audioStreamPlayer.Stream = sound;
		_audioStreamPlayer.Play();
	}

	public void StopSounds() {
		_audioStreamPlayer.Stop();
	}

	public void PlayAnim(string animName, Callable? finishedCalledback = null, Callable? cancelledCallback = null) {
		var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		if (_lastPlayedAnim != null && GetAnim() == _lastPlayedAnim && _currentAnimCancelledCallback is Callable cancelledcall)
			cancelledcall.Call();

		if (animPlayer==null || !animPlayer.HasAnimation(animName)) {
			if (finishedCalledback is Callable finishedcall) finishedcall.Call();
			return;
		} 

		_currentAnimFinishedCallback = finishedCalledback;
		_currentAnimCancelledCallback = cancelledCallback;
		_lastPlayedAnim = animName;
		animPlayer.ClearQueue();


		animPlayer.Seek(0f);
		animPlayer.Play(animName);
	}

	public void QueueAnim(string animName) {
		var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animPlayer==null || !animPlayer.HasAnimation(animName)) return;
		animPlayer.Queue(animName);
	}

	public void CurrentAnimChanged(string newAnim) {
		var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (newAnim != _lastPlayedAnim && _currentAnimFinishedCallback is Callable finishedcall)
			finishedcall.Call();
		_lastPlayedAnim = animPlayer.CurrentAnimation;
		if (_lastPlayedAnim != animPlayer.CurrentAnimation) {
			_currentAnimFinishedCallback = null;
			_currentAnimCancelledCallback = null;
		}
	}

	public string GetAnim() {
		var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animPlayer==null) return "";
		return animPlayer.CurrentAnimation;
	}
}
