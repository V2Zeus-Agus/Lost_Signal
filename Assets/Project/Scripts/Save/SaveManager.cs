using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Orquesta el guardado en dos niveles:
///  - Perfil (global): mochila, mejoras, tiempo jugado, planetas desbloqueados.
///  - Planeta actual: todos los SaveableEntity de la escena.
/// No guarda vida, oxígeno ni posición: al cargar aparecés en la nave a salvo.
/// </summary>
[DefaultExecutionOrder(100)] // carga después de que el HUD y demás hicieron su Start
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private PlayerUpgrades playerUpgrades;

    [Header("Opciones")]
    [SerializeField] private bool loadOnStart = true;
    [Tooltip("Segundos de juego entre autoguardados. 0 = desactivado.")]
    [SerializeField, Min(0f)] private float autosaveInterval = 120f;
    [Tooltip("El autoguardado arranca recién cuando el jugador tiene una base con aire (hábitat + generador de O2).")]
    [SerializeField] private bool autosaveRequiresBase = true;

    /// <summary>¿Corre el autoguardado? Sin base con aire, solo se guarda al salir o al morir.</summary>
    public bool AutosaveActive =>
        autosaveInterval > 0f &&
        (!autosaveRequiresBase || (BaseBuildingManager.Instance != null && BaseBuildingManager.Instance.HasPressurizedBase));

    private ProfileSave profile;
    private float autosaveTimer;

    public ProfileSave Profile => profile;
    public float TotalPlayTime => profile != null ? profile.totalPlayTime : 0f;

    public event Action OnSaved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        profile = loadOnStart ? SaveSystem.LoadProfile() : null;
        bool hasSave = profile != null;
        if (profile == null) profile = new ProfileSave();

        string planetId = PlanetContext.CurrentId;
        profile.currentPlanetId = planetId;
        if (!profile.unlockedPlanets.Contains(planetId)) profile.unlockedPlanets.Add(planetId);

        if (!hasSave) return;

        ApplyProfile();

        PlanetSave planetSave = SaveSystem.LoadPlanet(planetId);
        if (planetSave != null) ApplyPlanet(planetSave);

        NotificationQueue.Enqueue("Partida cargada");
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            profile.totalPlayTime += Time.deltaTime;

            if (AutosaveActive)
            {
                autosaveTimer += Time.deltaTime;
                if (autosaveTimer >= autosaveInterval)
                {
                    autosaveTimer = 0f;
                    SaveGame();
                    NotificationQueue.Enqueue("Autoguardado");
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
        {
            SaveGame();
            NotificationQueue.Enqueue("[DEBUG] Partida guardada");
        }
#endif
    }

    private void OnApplicationQuit() => SaveGame();

    public void SaveGame()
    {
        if (profile == null) return;

        if (playerInventory != null) profile.playerInventory = playerInventory.Container.ToData();
        if (playerUpgrades != null) profile.upgrades = playerUpgrades.ToData();
        SaveSystem.SaveProfile(profile);

        var planetSave = new PlanetSave
        {
            planetId = PlanetContext.CurrentId,
            lastVisitPlayTime = profile.totalPlayTime
        };

        foreach (SaveableEntity entity in FindAllEntities())
        {
            if (string.IsNullOrEmpty(entity.Id))
            {
                Debug.LogWarning($"[SaveManager] {entity.name} no tiene id de guardado. Usá Lost Signal > Fase 0 > Reasignar IDs de guardado.", entity);
                continue;
            }
            planetSave.entities.Add(entity.Capture());
        }

        SaveSystem.SavePlanet(planetSave);
        OnSaved?.Invoke();
    }

    private void ApplyProfile()
    {
        if (playerUpgrades != null) playerUpgrades.LoadData(profile.upgrades);
        if (playerInventory != null) playerInventory.Container.LoadData(profile.playerInventory, itemDatabase);
    }

    private void ApplyPlanet(PlanetSave planetSave)
    {
        var records = new Dictionary<string, EntityRecord>();
        foreach (EntityRecord record in planetSave.entities)
            if (!string.IsNullOrEmpty(record.id)) records[record.id] = record;

        foreach (SaveableEntity entity in FindAllEntities())
        {
            if (!string.IsNullOrEmpty(entity.Id) && records.TryGetValue(entity.Id, out EntityRecord record))
                entity.Restore(record, itemDatabase);
        }
    }

    // Incluye inactivos: un nodo agotado está desactivado pero igual hay que guardarlo.
    private static SaveableEntity[] FindAllEntities() =>
        FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
}
