using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public const string SensitivityPrefKey = "options.sensitivity";

    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Stamina stamina;
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.7f;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float jumpHeight = 2.4f;
    [SerializeField] private float gravity = -10f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private float stepLength = 2.2f;

    private CharacterController characterController;
    private Vector2 moveInput;
    private bool wantsToSprint;
    private float pitch;
    private float verticalVelocity;
    private float distanceSinceStep;

    public event Action OnJumped;
    public event Action OnFootstep;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        lookSensitivity = PlayerPrefs.GetFloat(SensitivityPrefKey, lookSensitivity);
    }

    public float LookSensitivity
    {
        get => lookSensitivity;
        set => lookSensitivity = value;
    }

    public void SetMoveInput(Vector2 input) => moveInput = input;

    public void SetSprint(bool sprinting) => wantsToSprint = sprinting;

    public void Look(Vector2 delta)
    {
        transform.Rotate(Vector3.up, delta.x * lookSensitivity);
        pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -maxPitch, maxPitch);
        cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    public void Jump()
    {
        if (!characterController.isGrounded) return;
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        OnJumped?.Invoke();
    }

    private void Update()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        bool sprinting = wantsToSprint && moveInput.y > 0.1f && stamina.CanSprint;
        stamina.SetSprinting(sprinting);

        float speed = sprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;
        move.y = verticalVelocity;
        characterController.Move(move * Time.deltaTime);

        if (characterController.isGrounded && moveInput.sqrMagnitude > 0.01f)
        {
            distanceSinceStep += speed * moveInput.magnitude * Time.deltaTime;
            if (distanceSinceStep >= stepLength)
            {
                distanceSinceStep = 0f;
                OnFootstep?.Invoke();
            }
        }
    }
}
