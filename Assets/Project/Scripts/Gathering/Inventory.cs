using System;
using UnityEngine;

/// <summary>
/// Mochila del jugador. Envuelve un ItemContainer con slots limitados.
/// Mantiene la misma API que antes (GetAmount / Add / Remove / OnResourceChanged)
/// para no romper el HUD ni las estaciones.
/// </summary>
public class Inventory : MonoBehaviour
{
    [SerializeField, Min(1)] private int slotCount = 12;
    [Tooltip("Máximo de items de la mochila sin mejoras, sumando todo. 0 = sin tope.")]
    [SerializeField, Min(0)] private int maxItems = 30;
    [Tooltip("Items extra por cada nivel de la mejora de mochila.")]
    [SerializeField, Min(0)] private int itemsPerBackpackLevel = 10;
    [Tooltip("Tope absoluto de la mochila con todas las mejoras.")]
    [SerializeField, Min(0)] private int absoluteMaxItems = 50;
    [SerializeField] private PlayerUpgrades upgrades;

    /// <summary>Se dispara cuando cambia la capacidad (mejora de mochila).</summary>
    public event Action OnCapacityChanged;

    private ItemContainer container;

    public event Action<ItemDefinition, int> OnResourceChanged;

    public ItemContainer Container
    {
        get
        {
            if (container == null)
            {
                container = new ItemContainer(slotCount, CurrentMaxItems());
                container.OnItemChanged += (item, total) => OnResourceChanged?.Invoke(item, total);
            }
            return container;
        }
    }

    public int MaxItems => Container.MaxTotalItems;

    public int BackpackLevel => upgrades != null ? upgrades.GetLevel(UpgradeIds.Backpack) : 0;

    private bool unlimitedOverride;

    /// <summary>Solo para la consola de desarrollador: saca el tope de items de la mochila.</summary>
    public bool UnlimitedOverride
    {
        get => unlimitedOverride;
        set
        {
            unlimitedOverride = value;
            Container.SetMaxTotalItems(CurrentMaxItems());
            OnCapacityChanged?.Invoke();
        }
    }

    private int CurrentMaxItems()
    {
        if (maxItems <= 0 || unlimitedOverride) return 0; // sin tope
        return Mathf.Min(maxItems + BackpackLevel * itemsPerBackpackLevel, Mathf.Max(absoluteMaxItems, maxItems));
    }

    private void Awake()
    {
        if (upgrades == null) upgrades = GetComponent<PlayerUpgrades>();
    }

    private void OnEnable()
    {
        if (upgrades != null) upgrades.OnUpgradeChanged += HandleUpgradeChanged;
    }

    private void OnDisable()
    {
        if (upgrades != null) upgrades.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    private void HandleUpgradeChanged(string upgradeId, int level)
    {
        if (upgradeId != UpgradeIds.Backpack) return;
        Container.SetMaxTotalItems(CurrentMaxItems());
        OnCapacityChanged?.Invoke();
    }
    public int TotalItems => Container.TotalCount;

    public int GetAmount(ItemDefinition type) => Container.GetAmount(type);

    public int GetSpaceFor(ItemDefinition type) => Container.GetSpaceFor(type);

    /// <summary>Devuelve cuánto entró realmente (puede ser menos si la mochila está llena).</summary>
    public int Add(ItemDefinition type, int amount) => Container.Add(type, amount);

    public int Remove(ItemDefinition type, int amount) => Container.Remove(type, amount);
}
