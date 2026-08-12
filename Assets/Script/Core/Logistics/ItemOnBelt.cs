using UnityEngine;

public class ItemOnBelt : MonoBehaviour
{
    [HideInInspector] public ItemData itemData;
    [HideInInspector] public ConveyorBelt currentBelt;
    [HideInInspector] public float progress;

    public void Init(ItemData data, ConveyorBelt belt, float startProgress = 0f)
    {
        itemData = data;
        currentBelt = belt;
        progress = Mathf.Clamp(startProgress, 0f, 1f);
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
    }

    void OnDestroy()
    {
        // Не трогаем belt.list если reshape/clear уже снял ссылку
        if (currentBelt != null)
            currentBelt.RemoveItem(this, destroyGameObject: false);
    }
}
