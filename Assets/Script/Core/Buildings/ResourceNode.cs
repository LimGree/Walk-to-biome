using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [Header("What can be mined here")]
    public ItemData resource;

    [Header("Optional")]
    public bool infinite = true;
    public int remainingAmount = 9999;

    public bool TryConsume(int amount = 1)
    {
        if (resource == null) return false;
        if (infinite) return true;

        if (remainingAmount < amount) return false;
        remainingAmount -= amount;
        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}