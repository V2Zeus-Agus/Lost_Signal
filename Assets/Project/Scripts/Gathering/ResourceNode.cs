using UnityEngine;

public class ResourceNode : MonoBehaviour, IInteractable
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int amountPerHarvest = 1;
    [SerializeField] private int harvestsLeft = 1;

    public string GetPrompt() => $"[E] Recolectar {resourceType.displayName}";

    public void Interact(Inventory inventory)
    {
        inventory.Add(resourceType, amountPerHarvest);
        NotificationQueue.Enqueue($"+{amountPerHarvest} {resourceType.displayName}");

        harvestsLeft--;
        if (harvestsLeft <= 0)
            gameObject.SetActive(false);
    }
}
