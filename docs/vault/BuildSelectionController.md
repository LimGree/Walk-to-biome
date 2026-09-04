# BuildSelectionController

**Файл:** `Assets/Script/Core/Player/BuildSelectionController.cs`

## Зачем нужен

Режим редактирования уже стоящих зданий (не путать со стройкой новых).

Умеет:

- рамкой выделить клетки;
- копировать группу;
- вставить копию (за деньги);
- перенести выделенное;
- повернуть группу;
- удалить выделенное.

Клавиши: SelectMode, Copy, Paste, MoveSelection (ещё и карта — см. конфликт ниже), Delete, Modifier, Rotate.

## Важно

Действие `MoveSelection` в [[InputSystem_Actions]] **то же самое**, что открытие карты в [[WorldMapUI]]. Поэтому карта не открывается в режиме стройки, а в обычном режиме это «карта». В режиме выделения та же клавиша — перенос. Смотри бинд «Карта / перенос».

Связи: [[PlayerBuilder]], [[GridOccupancy]], [[BuildingBase]], [[SelectionActionsUI]], [[GameAudio]] (`world_copy`, `world_paste`).
