using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// HUD ambiental (arriba a la derecha): zona, temperatura, presión, traje, refugio,
/// evento activo y porcentaje del planeta extraído a mano.
/// </summary>
public class EnvironmentHUD : MonoBehaviour
{
    [SerializeField] private PlayerUpgrades upgrades;
    [SerializeField] private ColdExposure cold;
    [SerializeField, Min(0.1f)] private float refreshSeconds = 0.5f;
    [SerializeField, Min(0.5f)] private float extractionRefreshSeconds = 3f;

    private TMP_Text text;
    private Canvas canvas;
    private float timer, extractionTimer;
    private float extractedPercent;
    private ResourceNode[] nodes;
    private readonly StringBuilder sb = new StringBuilder();

    private void Start()
    {
        if (upgrades == null) upgrades = FindFirstObjectByType<PlayerUpgrades>();
        if (cold == null && upgrades != null) cold = upgrades.GetComponent<ColdExposure>();
        nodes = FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        canvas = RuntimeUI.CreateCanvas("EnvironmentHUDCanvas", transform, 5);
        UnityEngine.UI.Image bg = RuntimeUI.Image("Panel", canvas.transform, new Color(0f, 0f, 0f, 0.4f), false);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -150f);
        rt.sizeDelta = new Vector2(410f, 210f);

        text = RuntimeUI.Text("Text", bg.transform, "", 20);
        RuntimeUI.Stretch(text.rectTransform, 12f);
        UpdateExtraction();
    }

    private void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        canvas.enabled = playing && !UIModal.IsOpen;
        if (!playing) return;

        extractionTimer += Time.deltaTime;
        if (extractionTimer >= extractionRefreshSeconds)
        {
            extractionTimer = 0f;
            UpdateExtraction();
        }

        timer += Time.deltaTime;
        if (timer < refreshSeconds) return;
        timer = 0f;
        Redraw();
    }

    private void UpdateExtraction()
    {
        int total = 0, left = 0;
        foreach (ResourceNode node in nodes)
        {
            if (node == null) continue;
            total += node.InitialHarvests;
            left += Mathf.Min(node.HarvestsLeft, node.InitialHarvests);
        }
        extractedPercent = total > 0 ? 100f * (total - left) / total : 0f;
    }

    private void Redraw()
    {
        sb.Clear();
        BiomeMap biomes = BiomeMap.Instance;
        PlanetDefinition planet = PlanetContext.Current;
        string planetName = planet != null ? planet.displayName.ToUpperInvariant() : "PLANETA";
        string zone = biomes != null && biomes.CurrentBiome != null ? biomes.CurrentBiome.displayName : "";
        sb.Append("<b>").Append(planetName).Append("</b>");
        if (!string.IsNullOrEmpty(zone)) sb.Append(" · ").Append(zone);
        sb.AppendLine();

        if (biomes != null)
        {
            float temp = biomes.CurrentTemperature;
            float limit = cold != null ? cold.CurrentLimit : SuitProgression.BaseColdLimit;
            string color = temp <= limit ? "#ff6655" : temp <= limit + 15f ? "#ffcc55" : "#ffffff";
            sb.Append("Temperatura: <color=").Append(color).Append('>').Append(temp.ToString("0")).Append(" °C</color>");
        }
        if (planet != null) sb.Append("   Presión: ").Append(planet.pressureKPa.ToString("0.0")).Append(" kPa");
        sb.AppendLine();

        int suit = upgrades != null ? upgrades.GetLevel(UpgradeIds.Suit) : 0;
        sb.Append("Traje nivel ").Append(suit).Append(" · aguanta ").Append(SuitProgression.ColdLimit(suit).ToString("0")).AppendLine(" °C");

        if (upgrades != null)
        {
            IShelter shelter = ShelterRegistry.Find(upgrades.transform.position);
            if (shelter != null) sb.Append("<color=#7fd4ff>Adentro: ").Append(shelter.ShelterName).AppendLine("</color>");
            else
            {
                Habitat unpressurized = Habitat.FindContaining(upgrades.transform.position);
                if (unpressurized != null) sb.AppendLine("<color=#ffcc55>Hábitat sin aire: falta un generador de O2</color>");
            }
        }

        EnvironmentEventManager events = EnvironmentEventManager.Instance;
        if (events != null && events.Current != null)
        {
            int s = Mathf.CeilToInt(events.Current.Remaining);
            sb.Append("<color=#ff8866>").Append(events.Current.DisplayName).Append(" · ")
              .Append(s / 60).Append(':').Append((s % 60).ToString("00")).AppendLine("</color>");
        }

        sb.Append("Extraído a mano: ").Append(extractedPercent.ToString("0")).Append('%');
        if (SaveManager.Instance != null)
            sb.AppendLine().Append(SaveManager.Instance.AutosaveActive
                ? "<color=#7fff9a>Autoguardado activo</color>"
                : "<color=#9aa>Sin autoguardado: construí una base con aire</color>");
        text.text = sb.ToString();
    }
}
