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
    private float timer, extractionTimer;
    private float extractedPercent;
    private ResourceNode[] nodes;
    private readonly StringBuilder sb = new StringBuilder();

    private void Start()
    {
        if (upgrades == null) upgrades = FindFirstObjectByType<PlayerUpgrades>();
        if (cold == null && upgrades != null) cold = upgrades.GetComponent<ColdExposure>();
        nodes = FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // Panel dentro de la columna derecha del HUD, debajo de los objetivos.
        RectTransform panel = HUDTheme.Panel("Environment", "Entorno", HUDTheme.Teal);
        HUDRoot.Instance.Add(HUDRoot.Instance.Right, panel, 10);
        text = HUDTheme.Label(panel, "", 17, HUDTheme.Text);
        text.lineSpacing = 6f;
        UpdateExtraction();
        Redraw();
    }

    private void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
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
        string muted = HUDTheme.Hex(HUDTheme.Muted);
        BiomeMap biomes = BiomeMap.Instance;
        PlanetDefinition planet = PlanetContext.Current;
        string planetName = planet != null ? planet.displayName.ToUpperInvariant() : "PLANETA";
        string zone = biomes != null && biomes.CurrentBiome != null ? biomes.CurrentBiome.displayName : "";
        sb.Append("<b><size=20>").Append(planetName).Append("</size></b>");
        if (!string.IsNullOrEmpty(zone)) sb.Append("  <color=").Append(muted).Append('>').Append(zone).Append("</color>");
        sb.AppendLine();

        if (biomes != null)
        {
            float temp = biomes.CurrentTemperature;
            float limit = cold != null ? cold.CurrentLimit : SuitProgression.BaseColdLimit;
            string color = temp <= limit ? HUDTheme.Hex(HUDTheme.Bad) : temp <= limit + 15f ? HUDTheme.Hex(HUDTheme.Warn) : "#ffffff";
            sb.Append("<color=").Append(muted).Append(">Temp.</color> <color=").Append(color).Append("><b>")
              .Append(temp.ToString("0")).Append(" °C</b></color>");
        }
        if (planet != null)
            sb.Append("    <color=").Append(muted).Append(">Presión</color> <b>").Append(planet.pressureKPa.ToString("0.0")).Append(" kPa</b>");
        sb.AppendLine();

        int suit = upgrades != null ? upgrades.GetLevel(UpgradeIds.Suit) : 0;
        sb.Append("<color=").Append(muted).Append(">Traje</color> <b>Nv ").Append(suit).Append("</b>  <color=").Append(muted)
          .Append(">aguanta hasta</color> <b>").Append(SuitProgression.ColdLimit(suit).ToString("0")).AppendLine(" °C</b>");

        if (upgrades != null)
        {
            IShelter shelter = ShelterRegistry.Find(upgrades.transform.position);
            if (shelter != null) sb.Append("<color=").Append(HUDTheme.Hex(HUDTheme.Teal)).Append(">● Adentro: ").Append(shelter.ShelterName).AppendLine("</color>");
            else
            {
                Habitat unpressurized = Habitat.FindContaining(upgrades.transform.position);
                if (unpressurized != null) sb.Append("<color=").Append(HUDTheme.Hex(HUDTheme.Warn)).AppendLine(">● Hábitat sin aire: falta un generador de O2</color>");
            }
        }

        EnvironmentEventManager events = EnvironmentEventManager.Instance;
        if (events != null && events.Current != null)
        {
            int s = Mathf.CeilToInt(events.Current.Remaining);
            sb.Append("<color=").Append(HUDTheme.Hex(HUDTheme.Bad)).Append("><b>▲ ").Append(events.Current.DisplayName).Append("</b>  ")
              .Append(s / 60).Append(':').Append((s % 60).ToString("00")).AppendLine("</color>");
        }

        sb.Append("<color=").Append(muted).Append(">Extraído a mano</color> <b>").Append(extractedPercent.ToString("0")).Append("%</b>");
        if (SaveManager.Instance != null)
            sb.AppendLine().Append(SaveManager.Instance.AutosaveActive
                ? "<color=" + HUDTheme.Hex(HUDTheme.Good) + "><size=15>● Autoguardado activo</size></color>"
                : "<color=" + muted + "><size=15>○ Sin autoguardado: construí una base con aire</size></color>");
        text.text = sb.ToString();
    }
}
