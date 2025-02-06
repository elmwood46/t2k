using Godot;
using System;

public partial class DestructibleChest : DestructibleMesh
{
    public InteractableComponent InteractableNode { get; set; }

    private bool _spawned_treasure = false;
    private bool _opened_chest = false;

    public const float MASS_WHEN_OPENED = 60.0f;

    public override void _Ready()
    {
        base._Ready();
        InteractableNode = (InteractableComponent)IntactScene.GetChild(0).GetChild(8);
        InteractableNode?.Connect("Interacted", Callable.From(OnInteracted));
        if (IntactScene?.GetChild(0).GetChild(6) is AnimationPlayer a)
        {
            a.AnimationFinished += (StringName anim_name) => {
                if (anim_name == "tween_glow_off")
                {
                    TurnGlowOff();
                }
            };
        }
    }

    public void SpawnTreasure()
    {
        GD.Print("treasure spawned");
    }

    public void OnInteracted()
    {
        if (!IsInsideTree()) return;
        if (_opened_chest) return;
        if (!base._is_broken && !_spawned_treasure)
        {
            _spawned_treasure = true;
            ((RigidBody3D)IntactScene.GetChild(0)).Mass = MASS_WHEN_OPENED;
            // create coins from opening the chest
            AddSibling(CoinSpawner.Create(((Node3D)IntactScene.GetChild(0)).GlobalPosition,3,2.0));
            GD.Print("Interacted with chest, spawning treasure");
            SpawnTreasure();
            // spawn treasure here
            var _t = new Timer() {
                WaitTime = 2.0,
                Autostart = false,
                OneShot = true
            };
            _t.Timeout += () => {
                FadeOutGlow();
            };
            AddSibling(_t);
            _t.Start();
        }
        SetOpened();
    }

    public void FadeOutGlow()
    {
        if (IntactScene?.GetChild(0).GetChild(6) is AnimationPlayer a)
        {
            a.Play("tween_glow_off");
        }
    }

    public bool IsOpened() => _opened_chest;
    public void SetOpened() {
        _opened_chest = true;
        _spawned_treasure = true;
    }

    public void TurnGlowOff()
    {
        ((OmniLight3D)IntactScene?.GetChild(0).GetChild(7)).LightEnergy = 0.0f;
    }

    public override void _PhysicsProcess(double delta)
    {

        if (base._is_broken && !_spawned_treasure)
        {
            // create coins from breaking the chest
            AddSibling(CoinSpawner.Create(BrokenScene.GlobalPosition,100,2.0));
            _spawned_treasure = true;
            GD.Print("Chest is broken, spawning treasure.");
            SpawnTreasure();
            // spawn treasure here
        }

        base._PhysicsProcess(delta);
    }
}
