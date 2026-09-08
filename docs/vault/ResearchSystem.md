# ResearchSystem

**Файл:** `Assets/Script/Core/Research/ResearchSystem.cs`

Дерево технологий. Узлы — [[ResearchNodeData]] из [[GameDatabase]].

## Цикл

1. Игрок в лаборатории выбирает узел, если предки открыты → `SetCurrentResearch`.
2. Предметы с ленты/`SubmitItem`: если предмет в списке нужного — копится. Набрали все — узел открыт, дают рубины, звук notify.
3. Открытый узел разрешает здания и рецепты (`unlockedBuildingIds` / рецепты с `requiredResearch`).

## Связи

[[ResearchLab]] · [[ResearchUI]] · [[MachineUI]] · [[PlayerWallet]] · [[BeltSpeedSystem]] · [[Economy]] · [[SaveData]] (ResearchSaveData)

Лишние шестерёнки после заполнения исследования всё равно идут в ленты, не пропадают.

Без выбранного узла лаборатория **продаёт** входящее за монеты. Поэтому в [[Обучение]] исследование стартует до лент.

Первая глава — `research_basic_automation` (125 Iron Ore + 125 Cooper ore). Вторая — `research_mechanical_engineering` (125 + 125 слитков).
