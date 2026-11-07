using System;
using UnityEngine;

public class Stamina : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("Gasto por segundo corriendo. 6.25 = dura el cuádruple que antes (25).")]
    [SerializeField] private float drainPerSecond = 6.25f;
    [SerializeField] private float regenPerSecond = 24f;
    [SerializeField] private float regenDelay = 1f;
    [SerializeField] private float minToSprintAgain = 20f;

    public float Current { get; private set; }
    public float Max => maxStamina;
    public bool CanSprint => Current > 0f && !exhausted;

    public event Action<float, float> OnStaminaChanged;

    private bool exhausted;
    private bool sprinting;
    private float regenTimer;

    private void Awake()
    {
        Current = maxStamina;
    }

    public void SetSprinting(bool isSprinting) => sprinting = isSprinting;

    private void Update()
    {
        float before = Current;

        if (sprinting)
        {
            Current = Mathf.Max(Current - drainPerSecond * Time.deltaTime, 0f);
            regenTimer = regenDelay;
            if (Current <= 0f) exhausted = true;
        }
        else if (regenTimer > 0f)
        {
            regenTimer -= Time.deltaTime;
        }
        else
        {
            Current = Mathf.Min(Current + regenPerSecond * Time.deltaTime, maxStamina);
            if (exhausted && Current >= minToSprintAgain) exhausted = false;
        }

        if (!Mathf.Approximately(before, Current))
            OnStaminaChanged?.Invoke(Current, maxStamina);
    }
}
