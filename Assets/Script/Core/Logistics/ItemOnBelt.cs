using UnityEngine;

public class ItemOnBelt : MonoBehaviour
{
    [HideInInspector] public ItemData itemData;
    [HideInInspector] public ConveyorBelt currentBelt;
    [HideInInspector] public float progress;          // 0..1 по текущему сегменту

    // Вызывается, когда предмет создаётся
    public void Init(ItemData data, ConveyorBelt belt, float startProgress = 0f)
    {
        itemData = data;
        currentBelt = belt;
        progress = startProgress;

        // Можно здесь подставить меш/материал из data.worldPrefab
        // Пока просто оставляем как есть
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
    }
}