using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Builderment/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;                    // уникальный идентификатор (например "iron_ore")
    public string displayName;           // "Железная руда"
    [TextArea] public string description;

    [Header("Visuals")]
    public Sprite icon;                  // иконка для UI и инвентаря
    public GameObject worldPrefab;       // префаб предмета, который едет по конвейеру

    [Header("Settings")]
    public int maxStack = 100;
    public bool isFuel = false;          // можно ли использовать как топливо
    public float fuelValue = 0f;         // сколько секунд горит (если isFuel)
}