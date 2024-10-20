using Godot;
using System;

// Abstract class to represent a pawn in the game
// tracks all the necessary variables for an actor in combat
// A pawn tracks the base stats of a character, their armour and their HP
public abstract partial class Pawn : Prop
{
    #region attributes
    [Export] public int Mus { get; set; }
    [Export] public int Bra { get; set; }
    [Export] public int Riz { get; set; }
    [Export] public int Gno { get; set; }
    [Export] public int Money {get; set;}

    [Export]public Timer StunTimer { get; set; }

    [Export] public Faction PawnFaction = Faction.Wizards;

    [Export] public bool IsHostile = false;

    public void SetAttr(int mus, int bra, int riz, int gno)
    {
        Mus = mus;
        Bra = bra;
        Riz = riz;
        Gno = gno;
    }
    #endregion

    #region stats
    [ExportGroup("Set Stats Manually (-1 for automatic)")]
    [Export] public int SetAtk { get; set; } = -1;
    [Export] public int SetDef { get; set; } = -1;
    [Export] public int SetSav { get; set; } = -1;
    [Export] public int SetWard { get; set; } = -1;
    [Export] public int SetMov { get; set; } = -1;
    [Export]  public int SetMaxHp { get; set; } = -1;
    public int Atk { get; private set; } = 0;
    // def - reduces the physical damage taken each hit. decreases over time.
    public int Def { get; private set; } = 0;
    // saving throw - used to resist bad status effects
    public int Sav { get; private set; } = 0;
    // ward - reduces the damage taken from spells and ranged attacks
    public int Ward { get; private set; } = 0;
    // move - how many map units a pawn can move on their turn
    public int Mov { get; private set; } = 0;
    public int HP { get; private set; } = 1;
    public int MaxHP { get; private set; } = 1;

    public void UpdateAllStats() {
        UpdateHealth(SetMaxHp);
        UpdateAtk(SetAtk);
        UpdateDef(SetDef);
        UpdateSav(SetSav);
        UpdateWard(SetWard);
        UpdateMov(SetMov);
    }
    #endregion

    #region combat methods
    public void TakeDamage(int damage)
    {
        HP -= damage;
        if (HP <= 0)
        {
            Die();
        }
    }

    virtual public void Die()
    {
        // Implement death logic here
        GD.Print(Title, ": I'm dead!");
        QueueFree();
    }

    public void Heal(int amount)
    {
        HP += amount;
        if (HP > MaxHP)
        {
            HP = MaxHP;
        }
    }
    #endregion

    #region setters for stats
    private void UpdateHealth(int value)
    {
        if (value >= 0)
        {
            MaxHP = value;
        }
        else
        {
            MaxHP = 10 + Mus * 2;
        }
        HP = MaxHP;
    }

    // Update methods for the new properties
    public void UpdateAtk(int value)
    {
        if (value >= 0)
        {
            Atk = value;
        }
        else
        {
            Atk = Bra * 2;
        }
    }

    public void UpdateDef(int value)
    {
        if (value >= 0)
        {
            Def = value;
        }
        else
        {
            Def = 0;
        }
    }

    public void UpdateSav(int value)
    {
        if (value >= 0)
        {
            Sav = value;
        }
        else
        {
            Sav = Mus + Bra + Riz + Gno;
        }
    }

    public void UpdateWard(int value)
    {
        if (value >= 0)
        {
            Ward = value;
        }
        else
        {
            Ward = 5 + Gno * 2;
        }
    }

    public void UpdateMov(int value)
    {
        if (value >= 0)
        {
            Mov = value;
        }
        else
        {
            Mov = 6 + Mus;
        }
    }
    #endregion

    #region _ready()
    public override void _Ready()
    {
        StunTimer.OneShot = true;
        StunTimer.Autostart = false;
        if (Engine.IsEditorHint()) return; //don't run in editor
        UpdateAllStats();
        GD.Print("_Ready() pawn title: ", Title, " children: ", GetChildren());
        //if (Sprite==null) throw new Exception("AnimatedSprite3D not found for pawn: " + Name);
        //if (CollisionShape==null) throw new Exception("CollisionShape3D not found for pawn: " + Name);
        //if (Nav==null) throw new Exception("NavigationAgent3D not found for pawn: " + Name);
    }
    #endregion
}