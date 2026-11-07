using System;
using System.Collections.Generic;

// Datos planos que se escriben a JSON. Nada de referencias a assets: solo ids.

[Serializable]
public class ItemStackData
{
    public int slot;
    public string itemId;
    public int amount;
}

[Serializable]
public class ContainerData
{
    public List<ItemStackData> stacks = new List<ItemStackData>();
}

[Serializable]
public class UpgradeLevelData
{
    public string id;
    public int level;
}

/// <summary>Progreso global: viaja con el jugador entre planetas.</summary>
[Serializable]
public class ProfileSave
{
    public int version = 1;
    public string currentPlanetId;
    [UnityEngine.Tooltip("Segundos jugados en total. Las bases solo producen en tiempo jugado.")]
    public float totalPlayTime;
    public List<string> unlockedPlanets = new List<string>();
    public List<string> unlockedRecipes = new List<string>();
    public List<UpgradeLevelData> upgrades = new List<UpgradeLevelData>();
    public ContainerData playerInventory = new ContainerData();
    public ContainerData shipCargo = new ContainerData();
}

[Serializable]
public class ComponentRecord
{
    public string type;
    public string json;
}

[Serializable]
public class EntityRecord
{
    public string id;
    public List<ComponentRecord> components = new List<ComponentRecord>();
}

/// <summary>Estado de un planeta: se queda en ese planeta cuando te vas.</summary>
[Serializable]
public class PlanetSave
{
    public int version = 1;
    public string planetId;
    [UnityEngine.Tooltip("totalPlayTime del perfil la última vez que estuviste acá (para la producción fuera de pantalla).")]
    public float lastVisitPlayTime;
    public List<EntityRecord> entities = new List<EntityRecord>();
}
