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

    public GameState State { get; private set; } = GameState.Tutorial;
    public bool IsPlaying => State == GameState.Playing;

    public string DefeatTitle { get; private set; } = "TE ELIMINARON";
    public string DefeatMessage { get; private set; } = "Las criaturas te alcanzaron.";

    public event Action<GameState> OnStateChanged;

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
        if (shipRepair != null) shipRepair.OnRepaired += Win;
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

    private void Start() => SetState(GameState.Tutorial);

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
        if (IsPlaying) SetState(GameState.Defeat);
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
