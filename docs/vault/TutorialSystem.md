# TutorialSystem

**Файл:** `Assets/Script/Core/Tutorial/TutorialSystem.cs`

Мозг обучения. Шаги из [[Обучение]]. Вешается на [[GameManager]].

Новый мир (нет `save.json`) → шаг 0, хотбар пустой. Старые сейвы v8 и ниже считаются пройденными. Пропуск и финал пишутся в сейв мира, не в PlayerPrefs.

Пока идёт обучение, [[PlayerInventory]] не кладёт новые здания на хотбар сам.

Связи: [[TutorialUI]] · [[TutorialFx]] · [[SaveSystem]] · [[ResearchSystem]] · [[BuildSelectionController]]
