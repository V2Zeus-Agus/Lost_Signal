using UnityEngine;

/// <summary>Cofre construido: 20 slots sin tope de items. Se abre con E (StoragePanel).</summary>
public class StorageContainer : MonoBehaviour, IInteractable, IPieceState
{
    public const int SlotCount = 20;

    public ItemContainer Container { get; } = new ItemContainer(SlotCount);

    public string GetPrompt() => $"[E] Abrir cofre ({Container.TotalCount} items)";

    public void Interact(Inventory inventory) => StoragePanel.Show(this, inventory);

    public string CaptureExtra() => JsonUtility.ToJson(Container.ToData());

    public void RestoreExtra(string json, ItemDatabase database)
    {
        if (string.IsNullOrEmpty(json)) return;
        Container.LoadData(JsonUtility.FromJson<ContainerData>(json), database);
    }
}
