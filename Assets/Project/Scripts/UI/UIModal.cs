using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro de paneles que toman el control (inventario, consola de desarrollador...).
/// Mientras haya alguno abierto: el cursor queda libre y PlayerInputHandler no mueve al jugador.
/// Es estático para que cualquier panel lo use sin referencias cruzadas (como NotificationQueue).
/// </summary>
public static class UIModal
{
    private static readonly HashSet<object> openPanels = new HashSet<object>();

    /// <summary>Frame del último abrir/cerrar: evita que el mismo Esc cierre un panel y además pause.</summary>
    public static int LastChangeFrame { get; private set; } = -1;

    public static bool IsOpen => openPanels.Count > 0;

    public static event Action<bool> OnChanged;

    /// <summary>El clic izquierdo no dispara (p. ej. en modo construcción).</summary>
    public static bool FireBlocked { get; set; }

    /// <summary>Este Esc ya lo usó alguien (cancelar construcción): que no pause.</summary>
    public static void ConsumeEscape() => LastChangeFrame = Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openPanels.Clear();
        LastChangeFrame = -1;
        OnChanged = null;
        FireBlocked = false;
    }

    public static bool IsOpenBy(object owner) => openPanels.Contains(owner);

    /// <summary>Hay algún panel abierto que no sea este.</summary>
    public static bool IsOpenByOther(object owner) => openPanels.Count > (openPanels.Contains(owner) ? 1 : 0);

    public static void Open(object owner)
    {
        if (owner == null || !openPanels.Add(owner)) return;
        Changed();
    }

    public static void Close(object owner)
    {
        if (owner == null || !openPanels.Remove(owner)) return;
        Changed();
    }

    private static void Changed()
    {
        LastChangeFrame = Time.frameCount;
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        bool lockCursor = playing && !IsOpen;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
        OnChanged?.Invoke(IsOpen);
    }
}
