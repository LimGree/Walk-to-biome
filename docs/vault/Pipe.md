# Pipe

**Файл:** `Assets/Script/Core/Logistics/Pipe.cs`  
**Предок:** [[Conveyor]]

## Зачем нужен

Та же логика ленты, но возит **только жидкости** (`ItemData.isFluid`). Ставится линией, как конвейер.

Короткий класс: переопределяет приём предмета — отвергает твёрдые.

Связи: [[Conveyor]], [[ItemData]], [[BuildingData]] (`isPipe`).
