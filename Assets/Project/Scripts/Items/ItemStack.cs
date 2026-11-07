using System;

/// <summary>Un slot de un contenedor: qué item tiene y cuántos.</summary>
[Serializable]
public class ItemStack
{
    public ItemDefinition item;
    public int amount;

    public bool IsEmpty => item == null || amount <= 0;

    public void Clear()
    {
        item = null;
        amount = 0;
    }
}
