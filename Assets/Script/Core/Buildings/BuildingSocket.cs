using UnityEngine;

public enum SocketType
{
    Input,
    Output
}

/// <summary>
/// I/O точка здания. Связи считает BuildingLinker по соседним клеткам.
/// </summary>
public class BuildingSocket : MonoBehaviour
{
    public SocketType socketType = SocketType.Output;

    [Header("Connections")]
    public BuildingSocket connectedSocket;

    [Header("Visual (optional)")]
    public GameObject connectionIndicator;

    [Header("Debug")]
    public bool showDebug = false;

    public BuildingBase Owner => GetComponentInParent<BuildingBase>();

    public bool IsConnected => connectedSocket != null;

    public void ConnectSocket(BuildingSocket other)
    {
        if (other == null) return;

        if (connectedSocket != null && connectedSocket != other)
            DisconnectSocket();
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

    public void DisconnectSocket()
    {
        if (connectedSocket != null)
        {
            BuildingSocket other = connectedSocket;
            connectedSocket = null;
            other.connectedSocket = null;

            if (other.connectionIndicator != null)
                other.connectionIndicator.SetActive(false);
        }

        if (connectionIndicator != null)
            connectionIndicator.SetActive(false);
    }

    public void DisconnectAll()
    {
        DisconnectSocket();
    }

    public bool IsNearOwnerCenter()
    {
        BuildingBase owner = Owner;
        if (owner == null)
            return false;

        Vector3 local = owner.transform.InverseTransformPoint(transform.position);
        local.y = 0f;
        return local.sqrMagnitude < 0.04f;
    }

    /// <summary>
    /// Наружу от здания: по смещению сокета, а не по transform.forward
    /// (на префабах forward часто смотрит не туда).
    /// </summary>
    public Vector3 GetOutward()
    {
        BuildingBase owner = Owner;
        if (owner == null)
            return Flatten(transform.forward);

        Vector3 local = owner.transform.InverseTransformPoint(transform.position);
        local.y = 0f;

        if (local.sqrMagnitude < 0.04f)
            return Flatten(transform.forward);

        Vector3 localOut = Mathf.Abs(local.x) >= Mathf.Abs(local.z)
            ? new Vector3(Mathf.Sign(local.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(local.z));

        return Flatten(owner.transform.TransformDirection(localOut));
    }

    static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    void OnDestroy()
    {
        DisconnectAll();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = socketType == SocketType.Input ? Color.green : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}
