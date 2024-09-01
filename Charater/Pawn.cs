using Godot;

// Abstract class to represent a pawn in the game
// tracks all the necessary variables for an actor in combat
// A pawn tracks the base stats of a character, their armour and their HP

public abstract partial class Pawn : CharacterBody3D
{
	[ExportGroup("Attributes")]
	[Export]
	public int Mus { get; set; }
	[Export]
	public int Bra { get; set; }
	[Export]
	public int Riz { get; set; }
	[Export]
	public int Gno { get; set; }

	public void SetAttr(int mus, int bra, int riz, int gno)
	{
		Mus = mus;
		Bra = bra;
		Riz = riz;
		Gno = gno;
	}
	// New properties with default values of zero

	// atk - adds to the chance to hit and thè damage dealt
	[ExportSubgroup("Set Stats Manually (-1 for automatic)")]
	[Export]
	public int SetAtk { get; set; } = -1;
	[Export]
	public int SetDef { get; set; } = -1;
	[Export]
	public int SetSav { get; set; } = -1;
	[Export]
	public int SetWard { get; set; } = -1;
	[Export]
	public int SetMov { get; set; } = -1;
	[Export]
	public int SetMaxHp { get; set; } = -1;


	#region stats
	
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
	#endregion

	override public void _Ready()
	{
		UpdateHealth(SetMaxHp);
		UpdateAtk(SetAtk);
		UpdateDef(SetDef);
		UpdateSav(SetSav);
		UpdateWard(SetWard);
		UpdateMov(SetMov);
	}

	public void TakeDamage(int damage)
	{
		HP -= damage;
		if (HP <= 0)
		{
			Die();
		}
	}

	public void Die()
	{
		// Implement death logic here
		GD.Print("I'm dead!");
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
}