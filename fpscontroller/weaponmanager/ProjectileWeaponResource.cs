using Godot;
using System;

public partial class ProjectileWeaponResource : WeaponResource
{
    [Export] public PackedScene Projectile {get;  set;} // must be RigidBody3D to work
    [Export] public Vector3 ProjectileRelativeVelocity {get; set;}
    [Export] public Vector3 ProjectileRelativeSpawnPos {get; set;}
    [Export] public Vector3 ProjectileRelativeSpawnRot {get; set;}

    new private void FireShot() {
        WeaponManager.Instance.PlayAnim(ViewShootAnim);
        WeaponManager.Instance.PlaySound(ShootSound);
        WeaponManager.Instance.QueueAnim(ViewIdleAnim);

        var raycast = WeaponManager.Instance.BulletRaycast;
        Vector2 recoil = WeaponManager.GetCurrentRecoil();
        raycast.Rotation = new Vector3(recoil.X, recoil.Y, raycast.Rotation.Z); 
        raycast.TargetPosition = ProjectileRelativeSpawnPos;
        raycast.ForceRaycastUpdate();

        var relSpawnPos = ProjectileRelativeSpawnPos;
        if (raycast.IsColliding()) // hit a wall
        {
            // dont spawn in wall
            relSpawnPos = raycast.GlobalTransform.AffineInverse() * raycast.GetCollisionPoint();
            relSpawnPos = relSpawnPos.LimitLength(relSpawnPos.Length() - 0.5f); // make spawned obect closer to player so it's not in wall
        }

        RigidBody3D obj = (RigidBody3D)Projectile.Instantiate();
        Player.Instance.AddSibling(obj);

        obj.GlobalTransform = raycast.GlobalTransform * new Transform3D(
            Basis.FromEuler(ProjectileRelativeSpawnRot), relSpawnPos
        );
        obj.LinearVelocity = Player.Instance.Velocity + raycast.GlobalTransform.Basis * ProjectileRelativeVelocity;

        WeaponManager.Instance.ShowMuzzleFlash();
        WeaponManager.Instance.ApplyRecoil();

        _last_fire_time = Time.GetTicksMsec();
        CurrentAmmo--;
    }
}