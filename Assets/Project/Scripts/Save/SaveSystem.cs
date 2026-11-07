using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Lee y escribe los archivos de guardado:
///   saves/slot0/profile.json          → ProfileSave (global)
///   saves/slot0/planets/{id}.json     → PlanetSave (uno por planeta)
/// </summary>
public static class SaveSystem
{
    private const string SlotFolder = "slot0";

    public static string RootFolder => Path.Combine(Application.persistentDataPath, "saves", SlotFolder);
    private static string ProfilePath => Path.Combine(RootFolder, "profile.json");
    private static string PlanetPath(string planetId) => Path.Combine(RootFolder, "planets", planetId + ".json");

    public static bool HasProfile => File.Exists(ProfilePath);

    public static ProfileSave LoadProfile() => Read<ProfileSave>(ProfilePath);
    public static void SaveProfile(ProfileSave profile) => Write(ProfilePath, profile);

    public static PlanetSave LoadPlanet(string planetId) => Read<PlanetSave>(PlanetPath(planetId));
    public static void SavePlanet(PlanetSave planet) => Write(PlanetPath(planet.planetId), planet);

    public static void DeleteAll()
    {
        if (Directory.Exists(RootFolder)) Directory.Delete(RootFolder, true);
    }

    private static T Read<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] No se pudo leer {path}: {e.Message}");
            return null;
        }
    }

    private static void Write<T>(string path, T data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Escribe a un temporal y después reemplaza: si el juego se cierra a mitad,
            // no queda un archivo corrupto.
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            File.Copy(temp, path, true);
            File.Delete(temp);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] No se pudo escribir {path}: {e.Message}");
        }
    }
}
