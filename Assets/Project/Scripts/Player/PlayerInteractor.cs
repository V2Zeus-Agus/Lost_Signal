using System;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Inventory inventory;
    [SerializeField] private float range = 3f;
    [SerializeField] private LayerMask interactableLayers = ~0;

    private IInteractable currentTarget;

    public event Action<string> OnFocusChanged;

    private void Update()
    {
        IInteractable found = null;

        if (GameManager.Instance.IsPlaying)
        {
            Transform cam = playerCamera.transform;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, range,
                                interactableLayers, QueryTriggerInteraction.Ignore))
            {
                found = hit.collider.GetComponentInParent<IInteractable>();
            }
        }

        if (found != currentTarget)
        {
            currentTarget = found;
            RefreshPrompt();
        }
    }

    public void TryInteract()
    {
        if (currentTarget == null) return;
        currentTarget.Interact(inventory);
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        OnFocusChanged?.Invoke(currentTarget != null ? currentTarget.GetPrompt() : string.Empty);
    }
}
