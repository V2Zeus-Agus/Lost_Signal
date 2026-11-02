using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "SampleScene";
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        OnBackButton();
    }

    public void OnPlayButton() => SceneManager.LoadScene(gameScene);

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
