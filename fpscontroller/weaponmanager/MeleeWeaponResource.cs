using Godot;
using System;

public partial class MeleeWeaponResource : WeaponResource
{
    [Export] public float MaxHitDist = 2.5f;
    [Export] public AudioStream MissSound;

    new private void FireShot() {
        WeaponManager.Instance.PlayAnim(ViewShootAnim);
        WeaponManager.Instance.PlayAnim(ViewShootAnim);
        WeaponManager.Instance.QueueAnim(ViewIdleAnim);

        var raycast = WeaponManager.Instance.BulletRaycast;
        raycast.TargetPosition = new Vector3(0,0,-Mathf.Abs(MaxHitDist));
        raycast.ForceRaycastUpdate();

        var bullet_target_pos = raycast.GlobalTransform * raycast.TargetPosition;
        var raycast_dir = (bullet_target_pos - raycast.GlobalPosition).Normalized();

        if (raycast.IsColliding()) {
            WeaponManager.Instance.PlaySound(ShootSound);
            var obj = raycast.GetCollider();
            var nrml = raycast.GetCollisionNormal();
            var pt = raycast.GetCollisionPoint();
            bullet_target_pos = pt;
            BulletDecalPool.SpawnBulletDecal(pt, nrml, (Node3D)obj, raycast.GlobalBasis, GD.Load<Texture2D>("res://fpscontroller/weaponmanager/knifedecal.png"));
            if (obj is RigidBody3D r) {
                r.ApplyImpulse(-nrml * _rigidBodyPushForce / r.Mass, pt - r.GlobalPosition);
            }
            if (obj.HasMethod("TakeDamage")) {
                obj.Call("TakeDamage", Damage);
            }
        
        } else
            WeaponManager.Instance.PlaySound(MissSound);
        _last_fire_time = Time.GetTicksMsec();
    }
}