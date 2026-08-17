using UnityEngine;

public class BeltSpeedSystem : MonoBehaviour
{
    public static BeltSpeedSystem Instance { get; private set; }

    public int Level { get; private set; }
    public int GearsTowardNext { get; private set; }

    public event System.Action OnChanged;

    public int NextCost => Economy.BeltUpgradeCost(Level + 1);
    public float Multiplier => Economy.BeltMultiplier(Level);
    public float Progress01 => NextCost > 0 ? Mathf.Clamp01((float)GearsTowardNext / NextCost) : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResetToNewWorld();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetToNewWorld()
    {
        Level = 0;
        GearsTowardNext = 0;
        OnChanged?.Invoke();
    }

    public bool SubmitGear()
    {
        GearsTowardNext++;
        while (GearsTowardNext >= NextCost)
        {
            GearsTowardNext -= NextCost;
            Level++;
        }

        OnChanged?.Invoke();
        return true;
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.beltLevel = Level;
        save.beltGears = GearsTowardNext;
    }

    public void ApplySave(SaveData save)
    {
        if (save == null)
        {
            ResetToNewWorld();
            return;
        }

        Level = Mathf.Max(0, save.beltLevel);
        GearsTowardNext = Mathf.Max(0, save.beltGears);
        OnChanged?.Invoke();
    }
}
