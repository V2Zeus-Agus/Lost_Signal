using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Actions (InputSystem_Actions)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference switchWeaponAction;
    [SerializeField] private InputActionReference pauseAction;

    [Header("Receivers")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Weapon weapon;
    [SerializeField] private PlayerInteractor interactor;

    private ICommand fireCommand;
    private ICommand interactCommand;

    /// <summary>Teclas reales de cada acción (para la lista de controles del menú de pausa).</summary>
    public System.Collections.Generic.List<(string action, string keys)> ControlHints()
    {
        var list = new System.Collections.Generic.List<(string, string)>();
        void Add(string label, InputActionReference reference)
        {
            if (reference == null || reference.action == null) return;
            string keys = KeyboardKeys(reference.action);
            if (!string.IsNullOrEmpty(keys)) list.Add((label, keys));
        }
        Add("Moverse", moveAction);
        Add("Saltar", jumpAction);
        Add("Correr", sprintAction);
        Add("Disparar", attackAction);
        Add("Usar / interactuar", interactAction);
        list.Add(("Cambiar disparo", "Ruedita del mouse"));
        Add("Pausa", pauseAction);
        return list;
    }

    /// <summary>Solo teclado y mouse; de los compuestos (WASD / flechas) se muestra el primero.</summary>
    private static string KeyboardKeys(InputAction action)
    {
        var parts = new System.Collections.Generic.List<string>();
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding b = bindings[i];
            if (b.isComposite)
            {
                var composite = new System.Collections.Generic.List<string>();
                var seenParts = new System.Collections.Generic.HashSet<string>();
                bool keyboard = true;
                int j = i + 1;
                for (; j < bindings.Count && bindings[j].isPartOfComposite; j++)
                {
                    if (!IsKeyboardOrMouse(bindings[j].effectivePath)) keyboard = false;
                    // Un compuesto puede tener WASD y flechas a la vez: solo la primera tecla de cada parte.
                    if (seenParts.Add(bindings[j].name)) composite.Add(Human(bindings[j].effectivePath));
                }
                // Si están WASD y flechas juntas, se muestran solo las letras.
                if (composite.Exists(k => !k.Contains("Arrow"))) composite.RemoveAll(k => k.Contains("Arrow"));
                if (keyboard && composite.Count > 0 && parts.Count == 0) parts.Add(string.Join("/", composite));
                i = j - 1;
                continue;
            }
            if (b.isPartOfComposite || !IsKeyboardOrMouse(b.effectivePath)) continue;
            string key = Human(b.effectivePath);
            if (!parts.Contains(key)) parts.Add(key);
        }
        return string.Join(" / ", parts);
    }

    private static bool IsKeyboardOrMouse(string path) =>
        !string.IsNullOrEmpty(path) && (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"));

    private static string Human(string path)
    {
        string text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        switch (text)
        {
            case "Left Button": return "Clic izq.";
            case "Right Button": return "Clic der.";
            case "Space": return "Espacio";
            case "Left Shift": return "Shift";
            case "Escape": return "Esc";
            case "Enter": return "Enter";
            default: return text.Length == 1 ? text.ToUpperInvariant() : text;
        }
    }

    private void Awake()
    {
        fireCommand = new FireCommand(weapon);
        interactCommand = new InteractCommand(interactor);
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        attackAction.action.Enable();
        interactAction.action.Enable();
        switchWeaponAction.action.Enable();
        pauseAction.action.Enable();
    }

    private void Update()
    {
        // Con un panel abierto (inventario, consola) Esc lo cierra el panel, no pausa.
        bool panelOpen = UIModal.IsOpen || UIModal.LastChangeFrame == Time.frameCount;
        if (pauseAction.action.WasPressedThisFrame() && !panelOpen)
            GameManager.Instance.TogglePause();

        // El frame en que se cierra un panel tampoco cuenta: si no, la misma E lo vuelve a abrir.
        if (!GameManager.Instance.IsPlaying || panelOpen)
        {
            playerController.SetMoveInput(Vector2.zero);
            playerController.SetSprint(false);
            return;
        }

        playerController.SetMoveInput(moveAction.action.ReadValue<Vector2>());
        playerController.Look(lookAction.action.ReadValue<Vector2>());
        playerController.SetSprint(sprintAction.action.IsPressed());

        if (jumpAction.action.WasPressedThisFrame())
            playerController.Jump();

        // Cambio de disparo con la ruedita del mouse (la Q ya no se usa).
        Mouse mouse = Mouse.current;
        float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scroll) > 0.01f) weapon.CycleStrategy(scroll > 0f ? 1 : -1);



        bool fire = weapon.IsAutomatic ? attackAction.action.IsPressed() : attackAction.action.WasPressedThisFrame();
        if (fire && !UIModal.FireBlocked)
            fireCommand.Execute();



        if (interactAction.action.WasPerformedThisFrame())
            interactCommand.Execute();
    }
}
