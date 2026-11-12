using System.Collections.Generic;
using System.Text;

/// <summary>Formatea costos para carteles y avisos.</summary>
public static class CostText
{
    public static string Format(IReadOnlyList<ItemAmount> cost)
    {
        var sb = new StringBuilder();
        foreach (ItemAmount c in cost)
        {
            if (c.item == null) continue;
            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(c.amount).Append(' ').Append(c.item.displayName);
        }
        return sb.ToString();
    }

    public static string Missing(Inventory inventory, IReadOnlyList<ItemAmount> cost)
    {
        var sb = new StringBuilder();
        foreach (ItemAmount c in cost)
        {
            if (c.item == null) continue;
            int missing = c.amount - inventory.GetAmount(c.item);
            if (missing <= 0) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(missing).Append(' ').Append(c.item.displayName);
        }
        return sb.ToString();
    }

    public static bool CanAfford(Inventory inventory, IReadOnlyList<ItemAmount> cost) =>
        DevCheats.InfiniteMaterials || (inventory != null && inventory.Container.HasAll(cost));
}
