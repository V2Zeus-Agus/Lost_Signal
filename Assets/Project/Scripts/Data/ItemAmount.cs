using System;
using UnityEngine;

/// <summary>Un item y una cantidad. Se usa en recetas, costos y recompensas.</summary>
[Serializable]
public class ItemAmount
{
    public ItemDefinition item;
    [Min(1)] public int amount = 1;
}
