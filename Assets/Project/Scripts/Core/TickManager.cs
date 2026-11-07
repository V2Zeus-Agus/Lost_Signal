using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulación a ritmo fijo para las máquinas del planeta actual.
/// Un taladro no necesita Update() a 144 fps: con 10 ticks por segundo alcanza
/// y el costo no crece con el framerate. Se pausa cuando el juego no está en Playing.
/// </summary>
public class TickManager : MonoBehaviour
{
    public const int TicksPerSecond = 10;
    public const float TickInterval = 1f / TicksPerSecond;
    private const int MaxTicksPerFrame = 10; // evita la "espiral de la muerte" tras un tirón

    private static readonly List<ITickable> tickables = new List<ITickable>();
    private static readonly List<ITickable> pendingAdd = new List<ITickable>();
    private static readonly List<ITickable> pendingRemove = new List<ITickable>();

    public static long TickCount { get; private set; }

    private float accumulator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        tickables.Clear();
        pendingAdd.Clear();
        pendingRemove.Clear();
        TickCount = 0;
    }

    public static void Register(ITickable tickable)
    {
        if (tickable == null) return;
        pendingRemove.Remove(tickable);
        if (!tickables.Contains(tickable) && !pendingAdd.Contains(tickable)) pendingAdd.Add(tickable);
    }

    public static void Unregister(ITickable tickable)
    {
        if (tickable == null) return;
        pendingAdd.Remove(tickable);
        if (tickables.Contains(tickable) && !pendingRemove.Contains(tickable)) pendingRemove.Add(tickable);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        accumulator += Time.deltaTime;

        int ticksThisFrame = 0;
        while (accumulator >= TickInterval && ticksThisFrame < MaxTicksPerFrame)
        {
            accumulator -= TickInterval;
            RunTick();
            ticksThisFrame++;
        }

        if (ticksThisFrame == MaxTicksPerFrame) accumulator = 0f;
    }

    private void RunTick()
    {
        ApplyPending();
        TickCount++;

        for (int i = 0; i < tickables.Count; i++)
            tickables[i].Tick(TickInterval);
    }

    private static void ApplyPending()
    {
        if (pendingRemove.Count > 0)
        {
            foreach (ITickable t in pendingRemove) tickables.Remove(t);
            pendingRemove.Clear();
        }
        if (pendingAdd.Count > 0)
        {
            tickables.AddRange(pendingAdd);
            pendingAdd.Clear();
        }
    }

    private void OnDestroy()
    {
        tickables.Clear();
        pendingAdd.Clear();
        pendingRemove.Clear();
    }
}
