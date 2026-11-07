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

        if (!GameManager.Instance.IsPlaying || UIModal.IsOpen)
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

        if (switchWeaponAction.action.WasPressedThisFrame())
            weapon.NextStrategy();



        bool fire = weapon.IsAutomatic ? attackAction.action.IsPressed() : attackAction.action.WasPressedThisFrame();
        if (fire)
            fireCommand.Execute();



        if (interactAction.action.WasPerformedThisFrame())
            interactCommand.Execute();
    }
}
