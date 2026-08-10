using UnityEngine;

public enum SocketType
{
    Input,
    Output
}

public class BuildingSocket : MonoBehaviour
{
    public SocketType socketType = SocketType.Output;

    [Header("Connections")]
    public ConveyorBelt connectedBelt;
    public BuildingSocket connectedSocket;      // ← прямое соединение здание-здание

    [Header("Visual (optional)")]
    public GameObject connectionIndicator;

    [Header("Debug")]
    public bool showDebug = true;

    public void ConnectBelt(ConveyorBelt belt)
    {
        // Если уже есть прямое соединение — разрываем его
        if (connectedSocket != null)
            DisconnectSocket();

        connectedBelt = belt;

        if (connectionIndicator != null)
            connectionIndicator.SetActive(true);

        if (showDebug)
            Debug.Log($"[Socket] {name} подключён к ленте {belt?.name}");
    }

    public void ConnectSocket(BuildingSocket other)
    {
        if (other == null) return;

        // Разрываем старые связи
        if (connectedBelt != null)
            DisconnectBelt();
        if (connectedSocket != null && connectedSocket != other)
            DisconnectSocket();

        connectedSocket = other;
        other.connectedSocket = this;   // двусторонняя связь

        if (connectionIndicator != null)
            connectionIndicator.SetActive(true);

        if (showDebug)
            Debug.Log($"[Socket] {name} ↔ {other.name} (прямое соединение)");
    }

    public void DisconnectBelt()
    {
        if (showDebug && connectedBelt != null)
            Debug.Log($"[Socket] {name} отключён от ленты");

        connectedBelt = null;

        if (connectionIndicator != null && connectedSocket == null)
            connectionIndicator.SetActive(false);
    }

    public void DisconnectSocket()
    {
        if (connectedSocket != null)
        {
            if (showDebug)
                Debug.Log($"[Socket] {name} отключён от {connectedSocket.name}");

            var other = connectedSocket;
            connectedSocket = null;
            other.connectedSocket = null;

            if (other.connectionIndicator != null && other.connectedBelt == null)
                other.connectionIndicator.SetActive(false);
        }

        if (connectionIndicator != null && connectedBelt == null)
            connectionIndicator.SetActive(false);
    }

    public void DisconnectAll()
    {
        DisconnectBelt();
        DisconnectSocket();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = socketType == SocketType.Input ? Color.green : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}