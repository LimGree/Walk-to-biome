# ResearchSystem

**Файл:** `Assets/Script/Core/Research/ResearchSystem.cs`

Дерево технологий. Живой список узлов — `GameDatabase.researches` ([[GameDatabase]]), сцена дублирует его в `allResearchNodes`.

## Старт

`startingBuildings`: extractor, conveyor, research_lab. Стартовых рецептов нет.

Лимит лабораторий: `baseLabLimit` = 1, потолок 3. +1 слот за `research_extractor_2` и `research_assembler_2`.

## Цикл

1. В лаборатории (E или T) выбирают узел, если предки открыты → `SetCurrentResearch`.
2. Предметы с ленты / `SubmitItem`: если предмет в стоимости узла — копится. Набрали все — узел открыт.
3. Узел даёт здания (`unlockedBuildings`) и рецепты (`unlockedRecipes`).

Без выбранного узла лаборатория **продаёт** входящее за монеты. В [[Обучение]] исследование стартует до лент.

Старые паки (`research_basic_automation` и т.д.) в папке Research не в дереве. Старые сейвы с их id не мапятся — новая игра.

Цены и граф — лист «Стоимость лабы» в рабочем xlsx. Первая глава: `research_smelter` (140 iron_ore + 140 cooper_ore) → плавильня. Слитки — отдельные узлы после неё.

## Связи

[[ResearchLab]] · [[ResearchUI]] · [[MachineUI]] · [[ResearchTree]] · [[ResearchNodeData]] · [[PlayerWallet]] · [[BeltSpeedSystem]] · [[Economy]] · [[SaveData]]
