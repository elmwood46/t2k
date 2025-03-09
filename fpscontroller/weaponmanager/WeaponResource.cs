using Godot;
using System;

[GlobalClass]
public partial class WeaponResource : Resource
{
    [Export] public int Damage = 10;
    [Export] public int CurrentAmmo = 7;
    [Export] public int MagazineSize = 7;
    [Export] public int ReserveAmmo = 99;
    [Export] public int MaxReserveAmmo = 99;
    [Export] public bool AutoFire = false;
    [Export] public float MaxFireRateMs = 50f;
    [Export] public Curve2D SprayPattern;
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

    protected float _rigidBodyPushForce = 5f;

	public const float RAYCAST_DIST = 9999f;

    protected int _num_shots_fired = 0;
    protected ulong _last_fire_time = 0UL;

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
    protected bool _isEquipped = false;

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
    protected bool _triggerDown = false;

    public void OnProcess(float delta)
    {
        if (Player.Instance.IsDead) return;
        if (AutoFire && _triggerDown && Time.GetTicksMsec() - _last_fire_time > MaxFireRateMs)
        {
            if (CurrentAmmo > 0)
                FireShot();
        }
    }


    protected void OnTriggerDown() {
        if (Player.Instance.IsDead) return;
        bool canFireTime = _last_fire_time == 0ul || Time.GetTicksMsec() - _last_fire_time > MaxFireRateMs;
        if (canFireTime && CurrentAmmo > 0)
            Call(nameof(FireShot));
        //else if (CurrentAmmo == 0) ReloadPressed(); // play some kind of reload sound
    }

    protected void OnTriggerUp() {
        throw new NotImplementedException();
    }

    public void ReloadPressed() {
        if (Player.Instance.IsDead) return;
        if (ViewReloadAnim != null && WeaponManager.Instance.GetAnim() == ViewReloadAnim) return; // dont play animation if already reloading
        if (GetReloadAmount() <= 0) return;

        var cancelsounds = new Callable(WeaponManager.Instance, nameof(WeaponManager.Instance.StopSounds));
        var reloadcallback = new Callable(this, nameof(Reload));

        WeaponManager.Instance.PlayAnim(ViewReloadAnim, reloadcallback, cancelsounds);
        WeaponManager.Instance.QueueAnim(ViewIdleAnim);
        WeaponManager.Instance.PlaySound(ReloadSound);
    }

    public void Reload() {
        if (Player.Instance.IsDead) return;
        var amount = GetReloadAmount();
        if (amount <= 0) return;
        else if (MagazineSize == int.MaxValue || CurrentAmmo == int.MaxValue) CurrentAmmo = MagazineSize;
        else {
            CurrentAmmo += amount;
            ReserveAmmo -= amount;
        }
    }

    protected int GetReloadAmount() {
        var wishreload = MagazineSize - CurrentAmmo;
        return Math.Min(wishreload, ReserveAmmo);
    }


    protected void OnUnequip()
    {
        //throw new NotImplementedException();
        GD.Print("unequipping weapon", this);
    }

    protected void OnEquip()
    {
        if (Player.Instance != null && Player.Instance.IsDead) return;
        if (ViewEquipAnim != null && ViewEquipAnim != "") WeaponManager.Instance.PlayAnim(ViewEquipAnim);
        if (ViewIdleAnim != null && ViewIdleAnim != "")  WeaponManager.Instance.PlayAnim(ViewIdleAnim);
    }

    protected void FireShot() {
        if (Player.Instance.IsDead) return;
        WeaponManager.Instance.PlayAnim(ViewShootAnim);
        WeaponManager.Instance.PlaySound(ShootSound);
        WeaponManager.Instance.QueueAnim(ViewIdleAnim);
        WeaponManager.Instance.ShowMuzzleFlash();

        var raycast = WeaponManager.Instance.BulletRaycast;
        raycast.SetCollisionMaskValue(9, true);
        Vector2 recoil = WeaponManager.GetCurrentRecoil();
        raycast.Rotation = new Vector3(recoil.X, recoil.Y, raycast.Rotation.Z); 
        raycast.TargetPosition = new Vector3(0,0,-RAYCAST_DIST);
        raycast.ForceRaycastUpdate();
        var bullet_target_pos = raycast.GlobalTransform * raycast.TargetPosition;
        if (raycast.IsColliding())
        {
            var obj = raycast.GetCollider();
            var normal = raycast.GetCollisionNormal();
            var pos = raycast.GetCollisionPoint();
            bullet_target_pos = pos;

            BulletDecalPool.SpawnBulletDecal(pos, normal, (Node3D)obj, raycast.GlobalBasis, null);

            if (obj is RigidBody3D r) {
                r.ApplyImpulse(-normal * _rigidBodyPushForce/r.Mass, pos - r.GlobalPosition);
            }

            // inflict damage
            if (obj is IHurtable hurtable_obj) 
            {
                //GD.Print("dealing damage melee to " + hurtable_obj.GetType().Name);
                hurtable_obj.TakeDamage(Damage,DamageType.Physical);
            }

            // check for destructible object
            if (obj is PhysicsBody3D pb)
            {
				if (pb.GetParent().GetParent() is DestructibleMesh mesh)
                {
                    mesh.TakeDamage(Damage, DamageType.Physical);
                    if (mesh.Health <= 0) mesh.Break(raycast.GetCollisionPoint(),_rigidBodyPushForce);
                }
            }
        }
        if (_num_shots_fired%2==0) WeaponManager.Instance.MakeBulletTrail(bullet_target_pos);
        _num_shots_fired++;
        WeaponManager.Instance.ApplyRecoil();

        _last_fire_time = Time.GetTicksMsec();
        CurrentAmmo--;
    }
}