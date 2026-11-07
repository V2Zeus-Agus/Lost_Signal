using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mejoras persistentes del jugador (tanque de O2, traje, mochila...).
/// Se guardan en el perfil, así que se conservan al cambiar de planeta.
/// Los sistemas escuchan OnUpgradeChanged y recalculan sus valores.
/// </summary>
public class PlayerUpgrades : MonoBehaviour
{
    private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

    public event Action<string, int> OnUpgradeChanged;

    public int GetLevel(string upgradeId) =>
        levels.TryGetValue(upgradeId, out int level) ? level : 0;

    public void SetLevel(string upgradeId, int level)
    {
        level = Mathf.Max(level, 0);
        levels[upgradeId] = level;
        OnUpgradeChanged?.Invoke(upgradeId, level);
    }

    public void AddLevel(string upgradeId) => SetLevel(upgradeId, GetLevel(upgradeId) + 1);

    public List<UpgradeLevelData> ToData()
    {
        var data = new List<UpgradeLevelData>();
        foreach (KeyValuePair<string, int> pair in levels)
            data.Add(new UpgradeLevelData { id = pair.Key, level = pair.Value });
        return data;
    }

    public void LoadData(List<UpgradeLevelData> data)
    {
        levels.Clear();
        if (data == null) return;
        foreach (UpgradeLevelData entry in data)
            if (!string.IsNullOrEmpty(entry.id)) SetLevel(entry.id, entry.level);
    }
}
