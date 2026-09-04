# Walk of Industry — карта проекта

Как открыть в Obsidian: [[Как_открыть]]

Это оглавление «под капотом». Каждый файл — одна деталь игры. Клик по `[[Имя]]` открывает карточку.

Игра — фабрика от первого лица: ходишь по миру, ставишь здания на сетку, предметы едут по конвейерам, исследования открывают рецепты, монеты тратятся на постройки.

**С чего начать, если всё перепуталось:** [[Архитектура]] → [[GameManager]] → [[PlayerBuilder]] → [[BuildingBase]] → [[SaveSystem]]

Текущая альфа для тестеров: [[Альфа]] (сейчас **0.1.0-alpha.5**, сборка через [[AlphaBuild]]).

---

## Как устроены сцены

- [[MainMenu]] — главное меню (миры, настройки)
- [[LoadingScreen]] — экран загрузки между меню и игрой
- [[GameManager]] — «дирижёр» игровой сцены, пауза, подключение систем
- Сцены Unity: `Assets/Scenes/MainMenu.unity`, `Loading.unity`, `SampleScene.unity` (сама игра)

---

## Игрок и строительство

- [[PlayerMovement]] — ходьба, камера, прыжок, зум
- [[PlayerBuilder]] — режим стройки: поставить / снести / линия конвейеров
- [[PlayerInventory]] — хотбар зданий
- [[PlayerInteractor]] — кнопка «взаимодействие» по взгляду, интерфейс [[IInteractable]]
- [[BuildSelectionController]] — выделение, копирование, вставка, перенос группы зданий
- [[BuildGridVisualizer]] — сетка под ногами в режиме стройки (`BuildGridPlane`, не белый квадрат)
- [[BuildIoArrowVisualizer]] — стрелки входов/выходов у призрака здания

---

## Здания и производство

- [[BuildingBase]] — общий предок всех зданий
- [[CrafterBuilding]] — общий предок станков, которые крафтят по рецепту
- [[BuildingSocket]] / [[SocketType]] — «розетки» входа и выхода
- [[BuildingLinker]] — кто с кем соседствует на сетке
- [[BuildingData]] — карточка здания в каталоге (цена, префаб, размер)

Станки-крафтеры (почти близнецы, разный звук/модель):

- [[Assembler]] · [[Constructor]] · [[Smelter]] · [[ChemicalPlant]] · [[Refinery]]

Добыча:

- [[Extractor]] — руда/дерево/камень с жилы [[ResourceNode]]
- [[OilExtractor]] · [[WaterExtractor]]

Логистика:

- [[Conveyor]] · [[Pipe]] (труба = конвейер только для жидкостей)
- [[Splitter]] · [[RoboticArm]]
- [[StorageContainer]] · [[FluidStorageTank]]
- [[BeltItemView]] — как предмет выглядит на ленте
- [[BeltSpeedSystem]] — общая скорость всех лент (прокачка в лаборатории)

Прочее:

- [[ResearchLab]] — сдача предметов в исследования и продажа
- [[PowerGenerator]] — заготовка под топливо (логика питания зданий ещё не доведена)
- [[ResourceNode]] — жила ресурса в мире

---

## Сетка мира и ландшафт

- [[GridSystem]] — клетка ↔ метры
- [[GridFootprint]] — сколько клеток занимает здание
- [[GridOccupancy]] — кто стоит в какой клетке
- [[WorldBiomeMap]] / [[WorldBiome]] — лес, поле, горы, вода
- [[WorldResourceScatterer]] — расстановка жил и деревьев
- [[ResourceNodePrefabs]] — какие модели жил использовать
- [[RuntimeMaterials]] — материалы, которые точно есть в билде

---

## Экономика, исследования, сохранения

- [[Economy]] — цены продажи, стоимость лент, замедление крафта
- [[PlayerWallet]] — монеты и рубины
- [[ResearchSystem]] — дерево исследований
- [[ResearchNodeData]] · [[RecipeData]] · [[ItemData]] · [[ItemStack]] · [[ItemInstance]]
- [[GameDatabase]] — каталог всего (здания, предметы, рецепты, исследования)
- [[ProductionStats]] — сколько чего произведено
- [[SaveSystem]] · [[SaveData]] — запись мира в JSON
- [[WorldCatalog]] — список миров на диске

---

## Карта

- [[WorldMapUI]] — миникарта и полная карта
- [[MapMarkerSystem]] — метки и голограммы
- [[MapExploration]] — исследованные / неисследованные клетки
- [[MapSettings]] · [[MapSettingsUI]]
- [[MapIcons]] — стрелка игрока на карте

---

## Настройки и управление

- [[SettingsHub]] — вкладки настроек
- [[SettingsControls]] — тумблеры, слайдеры, чипы
- [[GameSettings]] — свет, небо, туман, дальность объектов 16–140 м, разрешение, FPS (PlayerPrefs)
- [[KeybindStore]] · [[KeybindSettingsUI]] · [[InputSystem_Actions]]
- [[IndustryPause]] — меню паузы
- [[UiLocale]] — русский / английский

---

## Интерфейс

- [[IndustryUi]] — фабрика кнопок и панелей UI Toolkit
- [[UiRuntime]] · [[UiTheme]] · [[UiFactory]] · [[UiMotion]]
- [[UiModal]] · [[UiNotification]] · [[UiTooltip]] · [[UiAudio]] · [[UiStatus]]
- [[BuildMenuUI]] · [[InventoryUI]] · [[MachineUI]] · [[ResearchUI]]
- [[OverlayUi]] · [[WalletHud]] · [[InputHintUI]] · [[CrosshairHud]]
- [[SelectionActionsUI]] · [[LoadingScreen]] · [[HotbarSlotView]] · [[BagBuildingCard]]
- [[GameHudIcons]] · [[GameBranding]]

---

## Звук

- [[GameAudio]] — клипы из `Resources/Audio/`, микшер, амбиент биомов

---

## Редактор и сборка (не часть игры для игрока)

- [[AlphaBuild]] — сборка Windows-альфы из меню Unity
- [[BuildingFootprintEditor]] — подсказки footprint в Scene View

---

## Данные и папки без кода

- [[Папки_и_ассеты]] — модели, иконки, рецепты, USS, сцены, звуки

---

## Как читать карточки

В каждой карточке:

1. **Зачем нужен** — одной фразой
2. **Связи** — кто с кем дружит (`[[ссылки]]`)
3. **Поля** — что помнит объект
4. **Методы** — что делает, по шагам, простым языком
