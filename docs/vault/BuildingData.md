# BuildingData

**Файл:** `Assets/Script/Core/Data/BuildingData.cs`  
**Тип:** ScriptableObject (меню Create → Builderment → Building Data)

Карточка здания, не само здание на сцене.

## Поля

- `id`, имя, описание, иконка
- `prefab` / `ghostPrefab` — что ставить и какой призрак
- `cornerPrefab` — если задан, это конвейер с углами
- `isPipe` — труба
- `size` — клетки footprint
- `buildCost`
- `requiresResourceNode` / `allowOnWater` / `requiresWater`

Живые экземпляры на сцене — префабы с [[BuildingBase]].
