using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ShipRepair shipRepair;
    [SerializeField] private Weapon weapon;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Stamina stamina;
    [SerializeField] private PlayerOxygen oxygen;

    [Header("Widgets")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Slider oxygenBar;
    [SerializeField] private TMP_Text oxygenText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private Slider repairBar;
    [SerializeField] private TMP_Text repairText;
    [SerializeField] private TMP_Text weaponText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text notificationText;

    [Header("Notifications")]
    [SerializeField] private float notificationDuration = 2f;

    private readonly StringBuilder builder = new StringBuilder();
    private float notificationTimer;



    private void OnEnable()
    {
        playerHealth.OnHealthChanged += UpdateHealth;
        inventory.OnResourceChanged += UpdateResources;
        shipRepair.OnProgressChanged += UpdateRepair;
        weapon.OnStrategyChanged += UpdateWeapon;
        interactor.OnFocusChanged += UpdatePrompt;
        stamina.OnStaminaChanged += UpdateStamina;
        oxygen.OnOxygenChanged += UpdateOxygen;
    }



    private void OnDisable()
    {
        playerHealth.OnHealthChanged -= UpdateHealth;
        inventory.OnResourceChanged -= UpdateResources;
        shipRepair.OnProgressChanged -= UpdateRepair;
        weapon.OnStrategyChanged -= UpdateWeapon;
        interactor.OnFocusChanged -= UpdatePrompt;
        stamina.OnStaminaChanged -= UpdateStamina;
        oxygen.OnOxygenChanged -= UpdateOxygen;
    }

    private void Start()
    {
        UpdateHealth(playerHealth.Current, playerHealth.Max);
        UpdateStamina(stamina.Current, stamina.Max);
        UpdateOxygen(oxygen.Current, oxygen.Max);
        UpdateResources(null, 0);
        UpdateRepair(shipRepair.Progress);
        UpdateWeapon(weapon.CurrentStrategyName);
        UpdatePrompt(string.Empty);
        notificationText.text = string.Empty;
    }

    private void Update()
    {
        ProcessNotifications();
    }




    private void ProcessNotifications()
    {
        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.deltaTime;
            if (notificationTimer <= 0f) notificationText.text = string.Empty;
            return;
        }

        if (NotificationQueue.TryDequeue(out string message))
        {
            notificationText.text = message;
            notificationTimer = notificationDuration;
        }
    }




    private void UpdateHealth(float current, float max)
    {
        healthBar.maxValue = max;
        healthBar.value = current;
    }

    private void UpdateStamina(float current, float max)
    {
        staminaBar.maxValue = max;
        staminaBar.value = current;
    }

    private void UpdateOxygen(float current, float max)
    {
        oxygenBar.maxValue = max;
        oxygenBar.value = current;
        string status = oxygen.InSafeZone ? " · recargando en la nave" : "";
        oxygenText.text = $"O2: {Mathf.CeilToInt(current)}/{max:0} s{status}";
        oxygenText.color = current <= max * 0.25f && !oxygen.InSafeZone ? new Color(1f, 0.35f, 0.3f) : Color.white;
    }

    private void UpdateResources(ResourceType changedType, int newAmount)
    {
        builder.Clear();
        builder.AppendLine("<b>PARA REPARAR LA NAVE</b>");
        foreach (RepairRequirement requirement in shipRepair.Requirements)
        {
            ResourceType type = requirement.resourceType;
            int delivered = shipRepair.GetDelivered(type);
            int missing = Mathf.Max(requirement.amount - delivered, 0);

            builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(type.uiColor)).Append('>')
                   .Append(type.displayName).Append("</color>: ")
                   .Append(delivered).Append('/').Append(requirement.amount).Append(" en la nave");
            if (missing > 0) builder.Append(" · faltan ").Append(missing);
            else builder.Append(" · <color=#5f5>completo</color>");
            builder.Append(" · ").Append(inventory.GetAmount(type)).AppendLine(" en mochila");
        }
        resourcesText.text = builder.ToString();
    }

    private void UpdateRepair(float progress)
    {
        repairBar.value = progress;
        repairText.text = $"Nave: {Mathf.RoundToInt(progress * 100f)}%";
        UpdateResources(null, 0);
    }

    private void UpdateWeapon(string strategyName) => weaponText.text = strategyName;

    private void UpdatePrompt(string prompt) => promptText.text = prompt;
}
