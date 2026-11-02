using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    public const string VolumePrefKey = "options.volume";
    private const float DefaultSensitivity = 0.1f;

    [Tooltip("Opcional: en el menú principal no hay jugador.")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text sensitivityValueText;
    [SerializeField] private TMP_Text volumeValueText;

    private void Start()
    {
        float sensitivity = PlayerPrefs.GetFloat(PlayerController.SensitivityPrefKey, DefaultSensitivity);
        float volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        volumeSlider.SetValueWithoutNotify(volume);
        ApplySensitivity(sensitivity);
        ApplyVolume(volume);

        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnDestroy()
    {
        sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
        volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
    }

    private void OnSensitivityChanged(float value)
    {
        ApplySensitivity(value);
        PlayerPrefs.SetFloat(PlayerController.SensitivityPrefKey, value);
    }

    private void OnVolumeChanged(float value)
    {
        ApplyVolume(value);
        PlayerPrefs.SetFloat(VolumePrefKey, value);
    }

    private void ApplySensitivity(float value)
    {
        if (playerController != null) playerController.LookSensitivity = value;
        sensitivityValueText.text = value.ToString("0.00");
    }

    private void ApplyVolume(float value)
    {
        AudioListener.volume = value;
        volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
