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
    public BuildingSocket connectedSocket;

    [Header("Visual (optional)")]
    public GameObject connectionIndicator;

    [Header("Debug")]
    public bool showDebug = false;

    /// <summary>Двусторонняя связь socket ↔ belt.</summary>
    public void ConnectBelt(ConveyorBelt belt)
    {
        if (belt == null) return;

        if (connectedSocket != null)
            DisconnectSocket();

        if (connectedBelt != null && connectedBelt != belt)
            DisconnectBelt();

        if (socketType == SocketType.Input)
        {
            if (belt.connectedInputSocket != null && belt.connectedInputSocket != this)
                belt.connectedInputSocket.DisconnectBeltOnly();
            belt.connectedInputSocket = this;
        }
        else
        {
            if (belt.connectedOutputSocket != null && belt.connectedOutputSocket != this)
                belt.connectedOutputSocket.DisconnectBeltOnly();
            belt.connectedOutputSocket = this;
        }

        connectedBelt = belt;

        if (connectionIndicator != null)
            connectionIndicator.SetActive(true);

        if (showDebug)
            Debug.Log($"[Socket] {name} подключён к ленте {belt.name}");
    }

    public void ConnectSocket(BuildingSocket other)
    {
        if (other == null) return;

        if (connectedBelt != null)
            DisconnectBelt();
        if (connectedSocket != null && connectedSocket != other)
            DisconnectSocket();

        if (other.connectedBelt != null)
            other.DisconnectBelt();
        if (other.connectedSocket != null && other.connectedSocket != this)
            other.DisconnectSocket();

        connectedSocket = other;
        other.connectedSocket = this;

        if (connectionIndicator != null)
            connectionIndicator.SetActive(true);
        if (other.connectionIndicator != null)
            other.connectionIndicator.SetActive(true);

        if (showDebug)
            Debug.Log($"[Socket] {name} ↔ {other.name}");
    }

    /// <summary>Полный disconnect: socket + belt refs.</summary>
    public void DisconnectBelt()
    {
        ConveyorBelt belt = connectedBelt;
        connectedBelt = null;

        if (belt != null)
        {
            if (belt.connectedInputSocket == this)
                belt.connectedInputSocket = null;
            if (belt.connectedOutputSocket == this)
                belt.connectedOutputSocket = null;

            if (showDebug)
                Debug.Log($"[Socket] {name} отключён от ленты {belt.name}");
        }

        RefreshIndicator();
    }

    /// <summary>
    /// Только сторона socket (belt refs уже обнулены снаружи).
    /// Избегает рекурсии ClearBeltLinks ↔ DisconnectBelt.
    /// </summary>
    public void DisconnectBeltOnly()
    {
        connectedBelt = null;
        RefreshIndicator();
    }

    public void DisconnectSocket()
    {
        if (connectedSocket != null)
        {
            BuildingSocket other = connectedSocket;
            connectedSocket = null;
            other.connectedSocket = null;

            if (other.connectionIndicator != null && other.connectedBelt == null)
                other.connectionIndicator.SetActive(false);
        }

        RefreshIndicator();
    }

    public void DisconnectAll()
    {
        DisconnectBelt();
        DisconnectSocket();
    }

    void RefreshIndicator()
    {
        if (connectionIndicator != null && connectedBelt == null && connectedSocket == null)
            connectionIndicator.SetActive(false);
    }

    void OnDestroy()
    {
        // Не вызывать DisconnectAll если belt уже уничтожается —
        // только снять свои ссылки на belt
        ConveyorBelt belt = connectedBelt;
        connectedBelt = null;
        if (belt != null)
        {
            if (belt.connectedInputSocket == this)
                belt.connectedInputSocket = null;
            if (belt.connectedOutputSocket == this)
                belt.connectedOutputSocket = null;
        }

        if (connectedSocket != null)
        {
            BuildingSocket other = connectedSocket;
            connectedSocket = null;
            if (other != null)
                other.connectedSocket = null;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = socketType == SocketType.Input ? Color.green : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}
