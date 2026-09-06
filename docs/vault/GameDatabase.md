# GameDatabase

**Файл:** `Assets/Script/Core/Data/GameDatabase.cs`  
**Тип:** ScriptableObject в `Resources/GameDatabase.asset`

Телефонная книга игры: массивы зданий, предметов, рецептов, исследований.

При старте игры (ещё до сцены) `Bootstrap` грузит ассет и строит словари по `id`.

Статические `FindItem("iron_ore")` и т.д. — отсюда. Если id опечатан, вернёт пусто и здание просто не выдаст предмет.

Связи: все данные [[BuildingData]], [[ItemData]], [[RecipeData]], [[ResearchNodeData]].
