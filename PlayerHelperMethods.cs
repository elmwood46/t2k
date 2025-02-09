using Godot;
using System;

#nullable enable

public partial class Player : CharacterBody3D
{
    public void HandleCrouch(float delta) {
        var wasCrouched = _is_crouched;
        if (Input.IsActionPressed("Crouch")) {
            _is_crouched = true;
        }
        else if (_is_crouched && !TestMove(GlobalTransform, new Vector3(0, CROUCH_TRANSLATE, 0))) {
            _is_crouched = false;
        }

        var tryCrouchJump = 0f;
        if (wasCrouched != _is_crouched && !IsOnFloor() && !_snappedToStairsLastFrame) {
            tryCrouchJump = _is_crouched ? CROUCH_JUMP_BOOST : -CROUCH_JUMP_BOOST;
        }
        if (tryCrouchJump != 0) {
            var result = new KinematicCollision3D();
            TestMove(GlobalTransform, new Vector3(0, tryCrouchJump, 0), result);
            Position += new Vector3(0,result.GetTravel().Y,0);
        }
    
        HeadCrouched.Position = _is_crouched ? new Vector3(0, -CROUCH_TRANSLATE, 0) : Vector3.Zero;
        ((CapsuleShape3D)CollisionShape.Shape).Height = _is_crouched ? _standing_height-CROUCH_TRANSLATE : _standing_height;
        CollisionShape.Position = new Vector3(0, ((CapsuleShape3D)CollisionShape.Shape).Height/2, 0);
    }

    public void AddRecoil(float pitch, float yaw) {
        TargetRecoil.X += pitch;
        TargetRecoil.Y += yaw;
    }
    public Vector2 GetCurrentRecoil() {
        return CurrentRecoil;
    }
    public void UpdateRecoil(float delta) {
        TargetRecoil = TargetRecoil.Lerp(Vector2.Zero, RECOIL_RECOVER_SPEED * delta);
        var prevRecoil=CurrentRecoil;
        CurrentRecoil = CurrentRecoil.Lerp(TargetRecoil, RECOIL_APPLY_SPEED * delta);
        var recoilDifference = CurrentRecoil - prevRecoil;

        Head.RotateY(recoilDifference.Y);
        Camera.RotateX(recoilDifference.X);
        Camera.Rotation = new Vector3(Mathf.Clamp(Camera.Rotation.X, -Mathf.Pi/2, Mathf.Pi/2),Camera.Rotation.Y,Camera.Rotation.Z);
        _cameraXRotation = Camera.Rotation.X;
    }

    public void UpdateAnimations() {
        if (!IsOnFloor() && !_snappedToStairsLastFrame) {
            _stateMachinePlayback.Travel("MidJump");
            return;
        }

         // get vector of players movement relative to player model
        var relVel = Head.GlobalBasis.Inverse() * (Velocity * new Vector3(1,0,1)) / GetMoveSpeed();
        var relVelXZ = new Vector2(relVel.X, -relVel.Z);

        if (Input.IsActionPressed("Sprint")) {
            _stateMachinePlayback.Travel("RunBlendSpace2D");
            _animationTree.Set("parameters/RunBlendSpace2D/blend_position", relVelXZ);
        }
        else {
            _stateMachinePlayback.Travel("WalkBlendSpace2D");
            _animationTree.Set("parameters/WalkBlendSpace2D/blend_position", relVelXZ);
        }
    }

    public float GetMoveSpeed() {
        if (Input.IsActionPressed("Sprint")) return SprintSpeed;
        return WalkSpeed;
    }

    public void HoldRigidBody() {
        throw new NotImplementedException("HoldRigidBody");
        /*
		// confirms the first collider is the player character body; if not, something is wrong 
		if (ShapeCast.GetCollisionCount() > 0 && ShapeCast.GetCollider(0) != this)
			return;

		for (int i = 0; i < ShapeCast.GetCollisionCount(); i++) {
            if (ShapeCast.GetCollider(i) is RigidBody3D r && r.Freeze == false) {
                r.LinearVelocity = 
                r.GlobalTransform = GlobalTransform;
                r.LinearVelocity = Velocity;
                r.AngularVelocity = Vector3.Zero;
                r.ApplyCentralImpulse(Velocity * 10);
            }
			var collider = ShapeCast.GetCollider(i) as Node;
			if (collider?.GetNodeOrNull("InteractableComponent") is InteractableComponent interactable)
				return interactable;
		}
		return;        */
    }

        // TODO holding rigid body
    public void UpdateHeldRigidBody() {
        throw new NotImplementedException("UpdateHeldRigidBody");
        if (_held_object == null) return;
        /*
        _heldRigidBody.GlobalTransform = GlobalTransform;
        _heldRigidBody.LinearVelocity = Velocity;
        _heldRigidBody.AngularVelocity = Vector3.Zero;*/
    }

	public InteractableComponent? GetInteractableComponentAtShapecast() {
		// confirms the first collider is the player character body; if not, something is wrong 
		if (ShapeCast.GetCollisionCount() > 0 && ShapeCast.GetCollider(0) != this)
			return null;

		for (int i = 0; i < ShapeCast.GetCollisionCount(); i++) {
			var collider = ShapeCast.GetCollider(i) as Node;
			if (collider?.GetNodeOrNull("InteractableComponent") is InteractableComponent interactable)
				return interactable;
		}
		return null;
	}

	public void UpdateViewAndWorldModelMasks() {
		SetCullLayerRecursive(GetNode<Node3D>("%HandWorldModel"), WORLD_MODEL_LAYER, false);
        SetCullLayerRecursive(GetNode<Node3D>("%WorldModel"), WORLD_MODEL_LAYER, false);
		SetCullLayerRecursive(GetNode<Node3D>("%HandViewModel"), VIEW_MODEL_LAYER, true);
		Camera.SetCullMaskValue(WORLD_MODEL_LAYER, false); // hide the world model layer
		//Camera.SetCullMaskValue(VIEW_MODEL_LAYER, false); // hide the view model layer -- e.g. for mirrors and other cameras
	}

    // Recursive method to process all child nodes
    private static void SetCullLayerRecursive(Node node, int cull_layer, bool disableShadows)
    {
        // Iterate over the current node's children
        foreach (Node child in node.GetChildren())
        {
            // Example: If it's a VisualInstance3D, modify properties or do other logic
            if (child is VisualInstance3D visual)
            {
                // Set Layer Mask (example action)
                visual.SetLayerMaskValue(1, false);  // Disable layer 1 (example)
                visual.SetLayerMaskValue(cull_layer, true);   // Enable layer 2 (example)
            }

			if (disableShadows && child is GeometryInstance3D g) {
				g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			}

            // If the child has its own children, recursively process them
            SetCullLayerRecursive(child, cull_layer, disableShadows);
        }
    }

    public void AlignWorldModelToLookDir() {
        var worldModel = GetNode<Node3D>("%WorldModel");
        worldModel.Rotation = new Vector3(0, Head.Rotation.Y, 0);
    }

    public void CheckForCoins()
    {
        var overlap = CoinPickupArea.GetOverlappingBodies();
        foreach (var obj in overlap)
        {
            if (obj is Coin coin && !coin.MoveTowardPlayer)
            {
                coin.MoveTowardPlayer = true;
            }
        }
    }

    public static void AddMoney(int amount)
    {
        Instance._money += amount;
    }

	public void PushAwayRigidBodies() {
		for (int i=0; i<GetSlideCollisionCount();i++) {
			var c = GetSlideCollision(i);
            var collider = c.GetCollider();
            //if ( collider is Chunk) GD.Print($"collided with chunk {collider}");
            if (collider is not Coin && collider is RigidBody3D r) {
				var push_dir = -c.GetNormal();
				var veldiff = Velocity.Dot(push_dir) - r.LinearVelocity.Dot(push_dir);
				veldiff = Mathf.Max(veldiff, 0f);
				var massratio = Mathf.Min(1f,Mass/r.Mass);
				push_dir.Y = 0f;
				var push_force = massratio * PushForce;
				r.ApplyImpulse(push_dir * push_force * veldiff, c.GetPosition() - r.GlobalPosition);
			}
		}
	}

	private void SaveCameraPosForSmoothing() {
		if (_savedCameraGlobalPos==_cameraPosReset) {
			_savedCameraGlobalPos = CameraSmooth.GlobalPosition;
		}
	}

	private void ResetCameraSmooth(float delta) {
		if (_savedCameraGlobalPos==_cameraPosReset) return;
		CameraSmooth.GlobalPosition = new Vector3 (CameraSmooth.GlobalPosition.X,_savedCameraGlobalPos.Y, CameraSmooth.GlobalPosition.Z);
		CameraSmooth.Position = new Vector3(CameraSmooth.Position.X,Mathf.Clamp(CameraSmooth.Position.Y, -0.7f, 0.7f),CameraSmooth.Position.Z);
		var move_amount = Mathf.Max(Velocity.Length() * delta, WalkSpeed/2 * delta);
		CameraSmooth.Position = new Vector3(CameraSmooth.Position.X,Mathf.Lerp(CameraSmooth.Position.Y, 0f, move_amount),CameraSmooth.Position.Z);
		if (CameraSmooth.Position.Y == 0f) {
			_savedCameraGlobalPos = _cameraPosReset;
		}
	}

    /// <summary>
    /// Add camera shake amount between 0 and 1, adds as a proportion of max camera shake.
    /// </summary>
    /// <param name="amount">The percentage (between 0 and 1) of max camera shake to add. A value of 0.2 adds 20% camera shake.</param>
    public void AddCameraShake(float amount) 
    {
        amount = Mathf.Clamp(amount, 0, 1);
        _camera_shake_amount = Mathf.Clamp((float)_camera_shake_amount+amount*MaxCameraShake, 0, MaxCameraShake);
    }

    public void DoCameraShake()
    {
        if (_camera_shake_amount <= 0) return;
        var shake = new Vector3((float)GD.RandRange(-_camera_shake_amount, _camera_shake_amount), (float)GD.RandRange(-_camera_shake_amount, _camera_shake_amount), (float)GD.RandRange(-_camera_shake_amount, _camera_shake_amount));
        CameraShake.Position = shake;
        _camera_shake_amount = Mathf.Max(_camera_shake_amount - CameraShakeDecay, 0);
        if (_camera_shake_amount == 0) CameraShake.Position = Vector3.Zero;
    }

	private bool SnapUpStairsCheck(float delta)
	{
		if (!(IsOnFloor() || _snappedToStairsLastFrame)) return false;
		if (Velocity.Y > 0 || (Velocity * new Vector3(1,0,1)).Length() == 0) return false;

		var expectedMoveMotion = Velocity * new Vector3(1, 0, 1) * delta;
		var stepPosWithClearance = GlobalTransform.Translated(expectedMoveMotion + new Vector3(0, MAX_STEP_HEIGHT * 1.2f, 0));

		var downCheckResult = new KinematicCollision3D();
		if (TestMove(stepPosWithClearance, new Vector3(0, -MAX_STEP_HEIGHT * 1.2f, 0), downCheckResult) && (downCheckResult.GetCollider() is StaticBody3D || downCheckResult.GetCollider() is Chunk))
		{
			var stepHeight = (stepPosWithClearance.Origin + downCheckResult.GetTravel() - GlobalPosition).Y;
			if (stepHeight > MAX_STEP_HEIGHT || stepHeight <= 0.01 || (downCheckResult.GetPosition() - GlobalPosition).Y> MAX_STEP_HEIGHT) return false;

			StairsAheadRay.GlobalPosition = downCheckResult.GetPosition() + new Vector3(0, MAX_STEP_HEIGHT, 0) + expectedMoveMotion.Normalized() * 0.1f;
			StairsAheadRay.ForceRaycastUpdate();

			if (StairsAheadRay.IsColliding() && !IsSurfaceTooSteep(StairsAheadRay.GetCollisionNormal()))
			{
				SaveCameraPosForSmoothing();
				GlobalPosition = stepPosWithClearance.Origin + downCheckResult.GetTravel();
				ApplyFloorSnap();
				_snappedToStairsLastFrame = true;
				return true;
			}
		}

		return false;
	}

	private void SnapDownToStairsCheck() {
		var didSnap = false;
		StairsBelowRay.ForceRaycastUpdate();
		var floorBelow = StairsBelowRay.IsColliding() && !IsSurfaceTooSteep(StairsBelowRay.GetCollisionNormal());
		var wasOnFloorLastFrame = Engine.GetPhysicsFrames() == _lastFrameOnFloor;
		if (!IsOnFloor() && Velocity.Y <= 0 && (wasOnFloorLastFrame || _snappedToStairsLastFrame) && floorBelow)
		{
			var bodyTestResult = new KinematicCollision3D();
			if (TestMove(GlobalTransform, new Vector3(0, -MAX_STEP_HEIGHT, 0), bodyTestResult))
			{
				SaveCameraPosForSmoothing();
				var translateY = bodyTestResult.GetTravel().Y;
				Position += new Vector3(0, translateY, 0);
				ApplyFloorSnap();
				didSnap = true;
			}
		}
		_snappedToStairsLastFrame = didSnap;
	}

	private bool IsSurfaceTooSteep(Vector3 normal)
	{
		return normal.AngleTo(Vector3.Up) > FloorMaxAngle;
	}

	private bool HandleLadderPhysics(float delta) { // move around on ladder and return true if on ladder
		// Keep track of whether already on ladder
        bool wasClimbingLadder = (_curLadderClimbing != null) && _curLadderClimbing.OverlapsBody(this);
        if (!wasClimbingLadder)
        {
            _curLadderClimbing = null;
            foreach (Node node in GetTree().GetNodesInGroup("ladder_area3d"))
            {
                if (node is Area3D ladder && ladder.OverlapsBody(this))
                {
                    _curLadderClimbing = ladder;
                    break;
                }
            }
        }

        if (_curLadderClimbing == null)
            return false;

        // Set up variables
        Transform3D ladderTransform = _curLadderClimbing.GlobalTransform;
        Vector3 posRelToLadder = ladderTransform.AffineInverse() * GlobalPosition;

        float forwardMove = Input.GetActionStrength("Forward") - Input.GetActionStrength("Back");
        float sideMove = Input.GetActionStrength("Right") - Input.GetActionStrength("Left");

        Vector3 ladderForwardMove = ladderTransform.AffineInverse().Basis * 
                                    GetViewport().GetCamera3D().GlobalTransform.Basis * 
                                    new Vector3(0, 0, -forwardMove);

        Vector3 ladderSideMove = ladderTransform.AffineInverse().Basis * 
                                 GetViewport().GetCamera3D().GlobalTransform.Basis * 
                                 new Vector3(sideMove, 0, 0);

        // Strafe velocity
        float ladderStrafeVel = ClimbSpeed * (ladderSideMove.X + ladderForwardMove.X);

        // Climb velocity
        float ladderClimbVel = ClimbSpeed * -ladderSideMove.Z;
        float upWish = new Vector3(0, 1, 0).Rotated(new Vector3(1, 0, 0), Mathf.DegToRad(-45))
                                          .Dot(ladderForwardMove);
        ladderClimbVel += ClimbSpeed * upWish;

        // Dismount logic
        bool shouldDismount = false;

        if (!wasClimbingLadder)
        {
            bool mountingFromTop = posRelToLadder.Y > _curLadderClimbing.GetNode<Node3D>("TopOfLadder").Position.Y;
            if (mountingFromTop)
            {
                if (ladderClimbVel > 0) {
					//GD.Print("dismounting from ladderClimbVel > 0 (mounting from top)");
					shouldDismount = true;
				}
            }
            else
            {
                if ((ladderTransform.AffineInverse().Basis * _wish_dir).Z >= 0) {
					//GD.Print("dismounting from ladderTransform.AffineInverse().Basis * _wish_dir).Z >= 0");
                    shouldDismount = true;
				}
            }

            if (posRelToLadder.Z > 0.1f) {
				//GD.Print("dismounting from Mathf.Abs(posRelToLadder.Z) > 0.1f");
				shouldDismount = true;
			}
        }

        if (IsOnFloor() && ladderClimbVel <= 0) {
			//GD.Print("dismounting from floor and no climb vel");
			shouldDismount = true;
		}

		GD.Print(ladderClimbVel);
		//GD.Print("currLadder ", _curLadderClimbing);
		//GD.Print("shoulddismount ", shouldDismount);

        if (shouldDismount)
        {
            _curLadderClimbing = null;
            return false;
        }

        // Jump off ladder mid-climb
        if (wasClimbingLadder && Input.IsActionJustPressed("Jump"))
        {
            Velocity = _curLadderClimbing.GlobalTransform.Basis.Z * JumpVelocity * 1.5f;
            _curLadderClimbing = null;
            return false;
        }

        Velocity = ladderTransform.Basis * new Vector3(ladderStrafeVel, ladderClimbVel, 0);

        // Snap player onto ladder
        posRelToLadder.Z = 0;
        GlobalPosition = ladderTransform * posRelToLadder;

		PushAwayRigidBodies();
        MoveAndSlide();
        return true;
	}

    private static Vector3 Headbob(float time) {
        var pos = Vector3.Zero;
        pos.Y = Mathf.Sin(time * BOB_FREQ) * BOB_AMP;
        pos.X = Mathf.Cos(time * BOB_FREQ / 2) * BOB_AMP;
        return pos;
    }

    public void TakeDamage(int damage, BlockDamageType damageType) {
        //if (IsDead) return;
        CurrentHealth -= damage;
        if (CurrentHealth <= 0) {
            CurrentHealth = 0;
            // TODO implement player death
            GD.Print("Player died");
        }
    }
}
