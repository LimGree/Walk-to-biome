# InputSystem_Actions

**Файлы:** `Assets/InputSystem_Actions.inputactions` + `Assets/InputSystem_Actions.cs`

## Это сгенерированный код

Файл `.cs` пишет Unity из ассета Input System. Подпись `auto-generated` в шапке. Править руками почти бесполезно: Unity перезапишет.

## Что внутри

Карта действий **Player**: Move, Look, Place, Demolish, Interact, Jump, Sprint, Pause, Rotate, BuildMode, HotbarScroll, Research, SelectMode, копипаст выделения, **MoveSelection (карта / перенос)**, Modifier, Delete, Inventory, Shop, SelectionPanel, **Zoom**.

Карта **UI**: навигация меню (почти не используется, UI Toolkit живёт своей жизнью).

Игра всегда ходит в этот ассет через [[KeybindStore]], не напрямую `new InputSystem_Actions()` в десяти местах.
