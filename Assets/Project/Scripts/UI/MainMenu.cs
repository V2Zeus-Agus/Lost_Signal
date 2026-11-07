using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Escena del juego real (la que guarda la partida).")]
    [SerializeField] private string gameScene = "Mars";
    [Tooltip("Escena del tutorial: es por donde arranca una partida nueva.")]
    [SerializeField] private string tutorialScene = "SampleScene";
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [Tooltip("Se muestra solo si hay una partida guardada.")]
    [SerializeField] private GameObject continueButton;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        OnBackButton();

        if (continueButton != null) continueButton.SetActive(SaveSystem.HasProfile);
    }

    /// <summary>Nueva partida: borra el guardado y empieza por el tutorial.</summary>
    public void OnNewGameButton()
    {
        SaveSystem.DeleteAll();
        SceneManager.LoadScene(tutorialScene);
    }

    public void OnContinueButton() => SceneManager.LoadScene(gameScene);

    // Compatibilidad con el botón "Jugar" que crea el Setup Scene original.
    public void OnPlayButton() => OnNewGameButton();

    public void OnOptionsButton()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void OnBackButton()
    {
        optionsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void OnQuitButton() => Application.Quit();
}
