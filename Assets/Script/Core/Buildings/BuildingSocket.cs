using UnityEngine;

public enum SocketType
{
    Input,
    Output
}

public class BuildingSocket : MonoBehaviour
{
    public SocketType socketType = SocketType.Output;
    public ConveyorBelt connectedBelt;

    [Header("Visual (optional)")]
    public GameObject connectionIndicator;

    [Header("Debug")]
    public bool showDebug = true;

    void OnEnable()
    {
        ConveyorNetwork.Instance?.RegisterSocket(this);
    }

    void OnDisable()
    {
        ConveyorNetwork.Instance?.UnregisterSocket(this);
    }

    public void ConnectBelt(ConveyorBelt belt)
    {
        connectedBelt = belt;

        if (connectionIndicator != null)
            connectionIndicator.SetActive(true);

        if (showDebug)
            Debug.Log($"[Socket] {name} подключён к ленте {belt.name}");
    }

    public void DisconnectBelt()
    {
        if (showDebug && connectedBelt != null)
            Debug.Log($"[Socket] {name} отключён от ленты");

        connectedBelt = null;

        if (connectionIndicator != null)
            connectionIndicator.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = socketType == SocketType.Input ? Color.green : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}
