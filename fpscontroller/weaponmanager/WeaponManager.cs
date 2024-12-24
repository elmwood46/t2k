using Godot;
using System;

public partial class WeaponManager : Node3D
{
	[Export] public RayCast3D BulletRaycast;

	[Export] public WeaponResource CurrentWeapon;

	[Export] public Node3D ViewModelContainer;
	[Export] public Node3D WorldModelContainer;

	[Export] public bool AllowShooting { get; set; } = true;

	private Node3D _current_weapon_view_model = null;
	private Node3D _current_weapon_world_model = null;

	private AudioStreamPlayer3D _audioStreamPlayer;




	public static WeaponManager Instance { get; private set; }

	private void UpdateWeaponModel() {
		if (CurrentWeapon == null) return;
		if (ViewModelContainer != null && CurrentWeapon.ViewModel != null) {
			_current_weapon_view_model = (Node3D)CurrentWeapon.ViewModel.Instantiate();
			ViewModelContainer.AddChild(_current_weapon_view_model);
			_current_weapon_view_model.Position = CurrentWeapon.ViewModelPos;
			_current_weapon_view_model.Rotation = CurrentWeapon.ViewModelRot;
			_current_weapon_view_model.Scale = CurrentWeapon.ViewModelScale;
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
        UpdateWeaponModel();
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
