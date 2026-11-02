using TMPro;
using UnityEngine;

public class GameScreens : MonoBehaviour
{
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private TMP_Text defeatTitle;
    [SerializeField] private TMP_Text defeatMessage;
    [SerializeField] private GameObject optionsPanel;

    private void Start()
    {
        GameManager.Instance.OnStateChanged += Show;
        Show(GameManager.Instance.State);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= Show;
    }

    private void Show(GameState state)
    {
        tutorialPanel.SetActive(state == GameState.Tutorial);
        pausePanel.SetActive(state == GameState.Paused);
        victoryPanel.SetActive(state == GameState.Victory);
        defeatPanel.SetActive(state == GameState.Defeat);
        if (state == GameState.Defeat)
        {
            defeatTitle.text = GameManager.Instance.DefeatTitle;
            defeatMessage.text = GameManager.Instance.DefeatMessage;
        }
        optionsPanel.SetActive(false);
    }

    public void OnTutorialContinueButton() => GameManager.Instance.StartGame();
    public void OnResumeButton() => GameManager.Instance.TogglePause();
    public void OnRestartButton() => GameManager.Instance.Restart();
    public void OnOptionsButton()
    {
        pausePanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void OnBackButton() => Show(GameManager.Instance.State);
    public void OnMainMenuButton() => GameManager.Instance.GoToMainMenu();
}
