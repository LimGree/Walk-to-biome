using UnityEngine;
using UnityEngine.InputSystem;

// Повесить на капсулу игрока (нужен CharacterController на этом же объекте).
// Камера должна быть ДОЧЕРНИМ объектом игрока (примерно на высоте головы),
// её нужно перетащить в поле cameraTransform.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public bool canMove = true;
    public bool canLook = true;

    [Header("Camera")]
    public Transform cameraTransform; // дочерняя камера
    public float mouseSensitivity = 200f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f; // наклон камеры вверх/вниз

    // Input System variables
    private InputSystem_Actions inputActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isSprinting;
    private bool jumpPressed;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Подписка на события
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        inputActions.Player.Sprint.performed += ctx => isSprinting = true;
        inputActions.Player.Sprint.canceled += ctx => isSprinting = false;

        inputActions.Player.Jump.performed += ctx => jumpPressed = true;
        inputActions.Player.Jump.canceled += ctx => jumpPressed = false;
    }

    void OnEnable()
    {
        inputActions?.Enable();
    }

    void OnDisable()
    {
        inputActions?.Disable();
    }

    void Update()
    {

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        if (canLook) HandleMouseLook();
        if (canMove) HandleMovement();

    }

    void HandleMouseLook()
    {
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        // Поворот тела игрока по горизонтали (yaw)
        transform.Rotate(Vector3.up * mouseX);

        // Наклон камеры по вертикали (pitch) — только камера, не всё тело
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f; // прижимает к земле, чтобы isGrounded не мигал

        // Движение
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        float speed = isSprinting ? sprintSpeed : walkSpeed;
        controller.Move(move * speed * Time.deltaTime);

        // Прыжок
        if (jumpPressed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = false; // Сбрасываем флаг, чтобы не прыгал каждый кадр
        }

        // Гравитация
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}