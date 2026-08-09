using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Settings")]
    public float interactDistance = 4f;
    public LayerMask interactLayer = ~0; // всё по умолчанию

    private InputSystem_Actions inputActions;
    private Camera cam;
    private IInteractable currentInteractable;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        cam = Camera.main;
    }

    void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Interact.performed += OnInteract;
    }

    void OnDisable()
    {
        inputActions.Player.Interact.performed -= OnInteract;
        inputActions.Disable();
    }

    void Update()
    {
        CheckForInteractable();
    }

    void CheckForInteractable()
    {
        currentInteractable = null;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            currentInteractable = hit.collider.GetComponentInParent<IInteractable>();

            // временный дебаг
            if (currentInteractable != null)
                Debug.Log($"[Interactor] Смотрю на: {hit.collider.name} → {currentInteractable.GetType().Name}");
        }
    }

    void OnInteract(InputAction.CallbackContext ctx)
    {
        Debug.Log($"[Interactor] Interact pressed. currentInteractable = {(currentInteractable != null ? currentInteractable.ToString() : "NULL")}");

        if (currentInteractable != null)
        {
            currentInteractable.Interact(gameObject);
        }
        else
        {
            Debug.LogWarning("[Interactor] Не нашёл IInteractable под прицелом");
        }
    }
}

// Простой интерфейс для всего, с чем можно взаимодействовать
public interface IInteractable
{
    void Interact(GameObject interactor);
}