using Godot;
using System;

public interface IHurtable
{
	void TakeDamage(int damage, DamageType type);
}

public interface ISaveStateLoadable
{
    void LoadSavedState();
}