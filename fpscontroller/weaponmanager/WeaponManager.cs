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

	public static WeaponManager Instance { get; private set; }

	private void UpdateWeaponModel() {
		if (CurrentWeapon == null) return;
		if (ViewModelContainer != null && CurrentWeapon.ViewModel != null) {
			_current_weapon_view_model = (Node3D)CurrentWeapon.ViewModel.Instantiate();
			_current_weapon_muzzle = FindNode3DRecursive(_current_weapon_view_model, "Muzzle");
			ViewModelContainer.AddChild(_current_weapon_view_model);
			_current_weapon_view_model.Position = CurrentWeapon.ViewModelPos;
			_current_weapon_view_model.Rotation = CurrentWeapon.ViewModelRot;
			_current_weapon_view_model.Scale = CurrentWeapon.ViewModelScale;
			ShaderUtils.ApplyClipAndFovShaderToViewModel(_current_weapon_view_model);
			_current_weapon_world_model.Position = CurrentWeapon.WorldModelPos;
			_current_weapon_world_model.Rotation = CurrentWeapon.WorldModelRot;
			_current_weapon_world_model.Scale = CurrentWeapon.WorldModelScale;
		}
		if (WorldModelContainer != null && CurrentWeapon.WorldModel != null) {
			_current_weapon_world_model = (Node3D)CurrentWeapon.WorldModel.Instantiate();
			WorldModelContainer.AddChild(_current_weapon_world_model);
		}
		CurrentWeapon.IsEquipped = true;
		if (Player.Instance.HasMethod("UpdateViewAndWorldModelMasks")) {
			Player.Instance.UpdateViewAndWorldModelMasks();
		}
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

	public override void _Process(double delta) // align position of muzzle flash
	{
		if (_current_weapon_muzzle != null) 
			_muzzleFlashEffect.GlobalPosition = _current_weapon_muzzle.GlobalPosition;
	}

	public void PlaySound(AudioStream sound) {
		if (_audioStreamPlayer.Stream != sound) _audioStreamPlayer.Stream = sound;
		_audioStreamPlayer.Play();
	}

	public void StopSounds() {
		_audioStreamPlayer.Stop();
	}

	public void PlayAnim(string animName) {
		var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animPlayer==null || !animPlayer.HasAnimation(animName)) return;
		animPlayer.Seek(0f);
		animPlayer.Play(animName);
	}

	public void QueueAnim(string animName) {
		var animPlayer = _current_weapon_view_model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		if (animPlayer==null || !animPlayer.HasAnimation(animName)) return;
		animPlayer.Queue(animName);
	}
}
