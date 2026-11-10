using System;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Inventory inventory;
    [SerializeField] private float range = 3f;
    [SerializeField] private LayerMask interactableLayers = ~0;

    private IInteractable currentTarget;
    private float promptTimer;

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
            promptTimer = 0f;
        }
        else if (currentTarget != null)
        {
            // El texto puede cambiar mientras lo mirás (una fábrica que produce, un cofre que se llena).
            promptTimer += Time.deltaTime;
            if (promptTimer >= 0.5f)
            {
                promptTimer = 0f;
                RefreshPrompt();
            }
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
