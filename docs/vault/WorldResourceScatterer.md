# WorldResourceScatterer

**Файл:** `Assets/Script/Core/Systems/WorldResourceScatterer.cs`

Расставляет жилы и деревья по биомам после генерации [[WorldBiomeMap]].

Правило проекта: **одна жила — один тип руды** на гору/пятно. Цвет жилы потом рисуется на карте.

Список `Veins` читает [[WorldMapUI]] (точки на карте) и подсказки.

Префабы жил — [[ResourceNodePrefabs]], слой Unity `RESOURSES`.

## Прорисовка

Каждый кадр смотрит расстояние до игрока. Рендереры жил/деревьев дальше `visualRadius` выключаются. Радиус берётся из [[GameSettings.RenderDistance]] (16–140 м), не захардкожен.
