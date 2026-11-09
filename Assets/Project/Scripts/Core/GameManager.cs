using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Tutorial, Playing, Paused, Victory, Defeat }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Win / Lose conditions")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private ShipRepair shipRepair;

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Flujo de escena")]
    [Tooltip("Mostrar el panel de introducción/controles al empezar (el tutorial lo usa).")]
    [SerializeField] private bool showIntroPanel = true;
    [Tooltip("Si está activo, reparar la nave termina la escena con victoria (tutorial).")]
    [SerializeField] private bool endsOnShipRepaired = true;
    [Tooltip("Escena que se carga con 'Continuar' tras la victoria. Vacío = no hay siguiente.")]
    [SerializeField] private string nextSceneOnVictory = "";

    public GameState State { get; private set; } = GameState.Tutorial;
    public bool IsPlaying => State == GameState.Playing;

    public string DefeatTitle { get; private set; } = "TE ELIMINARON";
    public string DefeatMessage { get; private set; } = "Las criaturas te alcanzaron.";

    public event Action<GameState> OnStateChanged;

    /// <summary>Justo antes de pasar a Derrota (todavía en Playing): para soltar el cofre, guardar, etc.</summary>
    public event Action OnPlayerDefeated;



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        NotificationQueue.Clear();
    }


    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnDied += Lose;
        if (shipRepair != null && endsOnShipRepaired) shipRepair.OnRepaired += Win;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnDied -= Lose;
        if (shipRepair != null) shipRepair.OnRepaired -= Win;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start() => SetState(showIntroPanel ? GameState.Tutorial : GameState.Playing);

    public bool HasNextScene => !string.IsNullOrEmpty(nextSceneOnVictory);

    public void LoadNextScene()
    {
        if (!HasNextScene) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneOnVictory);
    }

    public void StartGame()
    {
        if (State == GameState.Tutorial) SetState(GameState.Playing);
    }

    public void TogglePause()
    {
        if (State == GameState.Playing) SetState(GameState.Paused);
        else if (State == GameState.Paused) SetState(GameState.Playing);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void SetDefeatCause(string title, string message)
    {
        DefeatTitle = title;
        DefeatMessage = message;
    }

    private void Win()
    {
        if (IsPlaying) SetState(GameState.Victory);
    }

    private void Lose()
    {
        if (!IsPlaying) return;
        OnPlayerDefeated?.Invoke();
        SetState(GameState.Defeat);
    }

    private void SetState(GameState newState)
    {
        State = newState;
        Time.timeScale = newState == GameState.Playing ? 1f : 0f;

        bool lockCursor = newState == GameState.Playing;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;

        OnStateChanged?.Invoke(newState);
    }
}
