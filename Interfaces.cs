using Godot;
using System;

public interface IHurtable
{
	void TakeDamage(int damage, BlockDamageType type);
}

public interface ISaveStateLoadable
{
    void LoadSavedState();
}