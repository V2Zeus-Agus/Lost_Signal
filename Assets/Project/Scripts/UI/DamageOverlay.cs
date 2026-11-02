using UnityEngine;
using UnityEngine.UI;

public class DamageOverlay : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private Image crack;
    [SerializeField] private Image redTint;
    [SerializeField, Range(0f, 1f)] private float threshold = 0.5f;

    private void OnEnable() => playerHealth.OnHealthChanged += UpdateOverlay;
    private void OnDisable() => playerHealth.OnHealthChanged -= UpdateOverlay;

    private void Start() => UpdateOverlay(playerHealth.Current, playerHealth.Max);

    private void UpdateOverlay(float current, float max)
    {
        bool damaged = max > 0f && current > 0f && current / max < threshold;
        crack.enabled = damaged;
        redTint.enabled = damaged;
    }
}
