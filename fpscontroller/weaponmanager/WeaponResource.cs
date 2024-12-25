using Godot;
using System;

public partial class WeaponResource : Resource
{
    [Export] public int Damage = 10;
    [Export] public PackedScene ViewModel;
    [Export] public PackedScene WorldModel;

    [Export] public Vector3 ViewModelPos;
    [Export] public Vector3 ViewModelRot;
    [Export] public Vector3 ViewModelScale = new(1,1,1);
    [Export] public Vector3 WorldModelPos;
    [Export] public Vector3 WorldModelRot; 
    [Export] public Vector3 WorldModelScale = new(1,1,1);

    [Export] public string ViewIdleAnim;
    [Export] public string ViewEquipAnim;
    [Export] public string ViewShootAnim;
    [Export] public string ViewReloadAnim;

    // sounds
    [Export] public AudioStream ShootSound;
    [Export] public AudioStream ReloadSound;
    [Export] public AudioStream UnholsterSound;

    public float RigidBodyPushForce = 5f;

	const float RAYCAST_DIST = 9999f;

    // weapon logic
    public bool IsEquipped
    {
        get => _isEquipped;
        set
        {
            if (_isEquipped != value)
            {
                _isEquipped = value;
                if (_isEquipped)
                {
                    OnEquip();
                }
                else
                {
                    OnUnequip();
                }
            }
        }
    }
    private bool _isEquipped = false;

    // weapon functions
    public bool TriggerDown
    {
        get => _triggerDown;
        set
        {
            if (_triggerDown != value)
            {
                _triggerDown = value;
                if (_triggerDown)
                {
                    OnTriggerDown();
                }
            }
        }
    }
    private bool _triggerDown = false;

    private void OnTriggerDown() {
        FireShot();
    }

    private void OnTriggerUp() {
        throw new NotImplementedException();
    }

    private void OnUnequip()
    {
        throw new NotImplementedException();
    }


    private void OnEquip()
    {
        WeaponManager.Instance.PlayAnim(ViewEquipAnim);
        WeaponManager.Instance.PlayAnim(ViewIdleAnim);
    }

    private void FireShot() {
        WeaponManager.Instance.PlaySound(ShootSound);
        WeaponManager.Instance.PlayAnim(ViewShootAnim);
        WeaponManager.Instance.QueueAnim(ViewIdleAnim);
        var raycast = WeaponManager.Instance.BulletRaycast;
        raycast.TargetPosition = new Vector3(0,0,-RAYCAST_DIST);
        raycast.ForceRaycastUpdate();
        if (raycast.IsColliding())
        {
            var obj = raycast.GetCollider();
            var normal = raycast.GetCollisionNormal();
            var pos = raycast.GetCollisionPoint();

            BulletDecalPool.SpawnBulletDecal(pos, normal, (Node3D)obj, raycast.GlobalBasis, null);

            if (obj is RigidBody3D r) {
                r.ApplyImpulse(-normal * RigidBodyPushForce/r.Mass, pos - r.GlobalPosition);
            }

            if (obj.HasMethod("TakeDamage")) {
                obj.Call("TakeDamage", Damage);
            }
        }
    }
}