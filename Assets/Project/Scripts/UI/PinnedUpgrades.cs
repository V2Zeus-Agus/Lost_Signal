using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mejoras y construcciones que el jugador fijó para seguir en el HUD (izquierda).
/// Claves: "upgrade:&lt;id&gt;" o "build:&lt;id&gt;". Se guardan en PlayerPrefs y avisan con
/// OnChanged (Observer) para que el HUD se redibuje.
/// </summary>
public static class PinnedUpgrades
{
    public const int MaxPins = 4;
    private const string PrefsKey = "LostSignal.Pins";

    private static List<string> pins;

    public static event Action OnChanged;

    public static IReadOnlyList<string> All
    {
        get
        {
            Load();
            return pins;
        }
    }

    public static string UpgradeKey(string upgradeId) => "upgrade:" + upgradeId;
    public static string BuildKey(string buildableId) => "build:" + buildableId;

    public static bool IsPinned(string key)
    {
        Load();
        return pins.Contains(key);
    }

    /// <summary>Fija o suelta. Si ya hay el máximo, suelta la más vieja.</summary>
    public static void Toggle(string key, string displayName)
    {
        Load();
        if (pins.Remove(key))
        {
            NotificationQueue.Enqueue($"Desfijado: {displayName}");
        }
        else
        {
            if (pins.Count >= MaxPins) pins.RemoveAt(0);
            pins.Add(key);
            NotificationQueue.Enqueue($"Fijado en el HUD: {displayName}");
        }
        Save();
    }

    public static void Unpin(string key)
    {
        Load();
        if (pins.Remove(key)) Save();
    }

    private static void Load()
    {
        if (pins != null) return;
        pins = new List<string>();
        string raw = PlayerPrefs.GetString(PrefsKey, "");
        foreach (string part in raw.Split('|'))
            if (!string.IsNullOrEmpty(part) && !pins.Contains(part)) pins.Add(part);
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, string.Join("|", pins));
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
