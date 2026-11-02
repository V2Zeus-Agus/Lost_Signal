using UnityEngine;

public class CreatureHealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private GameObject barRoot;
    [SerializeField] private RectTransform fill;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
        barRoot.SetActive(false);
    }

    private void OnEnable() => health.OnHealthChanged += UpdateBar;
    private void OnDisable() => health.OnHealthChanged -= UpdateBar;

    private void UpdateBar(float current, float max)
    {
        float ratio = max > 0f ? current / max : 0f;
        fill.anchorMax = new Vector2(ratio, 1f);

        barRoot.SetActive(ratio > 0f && ratio < 1f);
    }

    private void LateUpdate()
    {
        if (!barRoot.activeSelf) return;
        if (mainCamera == null) mainCamera = Camera.main;
        barRoot.transform.rotation = mainCamera.transform.rotation;
    }
}
