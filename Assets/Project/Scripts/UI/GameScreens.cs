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

    private PauseMenu pauseMenu;

    private void Start()
    {
        // La pausa se arma por código con el estilo del HUD; el panel viejo de la escena queda oculto.
        pauseMenu = gameObject.AddComponent<PauseMenu>();
        pauseMenu.Build(OnResumeButton, OnOptionsButton, OnRestartButton, OnMainMenuButton);
        foreach (GameObject panel in new[] { optionsPanel, victoryPanel, defeatPanel, tutorialPanel })
            ScreenStyler.Apply(panel);
        // El cambio de disparo pasó a la ruedita del mouse: se actualiza el texto de la intro.
        if (tutorialPanel != null)
            foreach (TMP_Text text in tutorialPanel.GetComponentsInChildren<TMP_Text>(true))
                text.text = text.text.Replace("2/Q cambiar arma", "Ruedita cambiar arma").Replace("Q cambiar arma", "Ruedita cambiar arma");

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
        pausePanel.SetActive(false);
        if (pauseMenu != null) pauseMenu.SetVisible(state == GameState.Paused);
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
        if (pauseMenu != null) pauseMenu.SetVisible(false);
        optionsPanel.SetActive(true);
    }

    public void OnBackButton() => Show(GameManager.Instance.State);
    public void OnMainMenuButton() => GameManager.Instance.GoToMainMenu();
    public void OnContinueButton() => GameManager.Instance.LoadNextScene();
}
