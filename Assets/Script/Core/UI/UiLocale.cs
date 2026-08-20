using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class UiLocale
{
    public const string PrefsKey = "UiLanguage";
    public const string Ru = "ru";
    public const string En = "en";

    public static event Action Changed;

    static string code;

    public static string Code
    {
        get
        {
            if (string.IsNullOrEmpty(code))
                code = PlayerPrefs.GetString(PrefsKey, Ru);
            if (code != En)
                code = Ru;
            return code;
        }
    }

    public static bool IsRu => Code == Ru;

    public static void Set(string next)
    {
        string value = next == En ? En : Ru;
        if (Code == value)
            return;
        code = value;
        PlayerPrefs.SetString(PrefsKey, code);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static string T(string key)
    {
        if (key != null && Table.TryGetValue(key, out var pair))
            return IsRu ? pair.ru : pair.en;
        return key ?? "";
    }

    public static string T(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public static VisualElement LanguageRow()
    {
        var row = IndustryUi.El("Lang", "lang-row");
        row.Add(IndustryUi.Text("L", T("settings.language"), "body-text", "grow"));
        Button ru = IndustryUi.Btn("RU", () => Set(Ru), "btn-small", "cat-chip");
        Button en = IndustryUi.Btn("EN", () => Set(En), "btn-small", "cat-chip");
        ru.RemoveFromClassList("btn");
        en.RemoveFromClassList("btn");
        IndustryUi.SetOn(ru, IsRu, "is-selected");
        IndustryUi.SetOn(en, !IsRu, "is-selected");
        row.Add(ru);
        row.Add(en);
        return row;
    }

    static readonly Dictionary<string, (string ru, string en)> Table =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            { "menu.continue", ("Продолжить", "Continue") },
            { "menu.worlds", ("Миры", "Worlds") },
            { "menu.settings", ("Настройки", "Settings") },
            { "menu.exit", ("Выход", "Exit") },
            { "menu.back", ("Назад", "Back") },
            { "menu.new_world", ("Новый мир", "New world") },
            { "menu.create", ("Создать", "Create") },
            { "menu.play", ("Играть", "Play") },
            { "menu.delete", ("Удалить", "Delete") },
            { "menu.empty_worlds", ("Пока нет миров", "No worlds yet") },
            { "menu.last_played", ("Последняя игра: {0}", "Last played: {0}") },
            { "menu.seed", ("Сид: {0}", "Seed: {0}") },
            { "menu.world_n", ("МИР {0}", "WORLD {0}") },
            { "menu.delete_title", ("Удалить мир?", "Delete world?") },
            { "menu.delete_body", ("Мир «{0}» будет удалён. Это нельзя отменить.", "World \"{0}\" will be deleted. This cannot be undone.") },
            { "menu.default_world", ("Мир {0}", "World {0}") },

            { "pause.title", ("Пауза", "Paused") },
            { "pause.resume", ("Продолжить", "Resume") },
            { "pause.save", ("Сохранить", "Save game") },
            { "pause.controls", ("Управление", "Controls") },
            { "pause.exit", ("В меню", "Exit to menu") },
            { "pause.exit_title", ("Выйти в меню?", "Exit to menu?") },
            { "pause.exit_body", ("Несохранённый прогресс может быть потерян.", "Unsaved progress may be lost.") },
            { "pause.esc", ("ESC — продолжить", "ESC — Resume") },
            { "pause.saved", ("Сохранено", "Saved") },

            { "settings.title", ("Настройки", "Settings") },
            { "settings.general", ("Общие", "General") },
            { "settings.gameplay", ("Игра", "Gameplay") },
            { "settings.controls", ("Управление", "Controls") },
            { "settings.language", ("Язык", "Language") },
            { "settings.minimap_zoom", ("Масштаб миникарты  {0}%", "Minimap zoom  {0}%") },
            { "settings.minimap_size", ("Размер миникарты  {0} px", "Minimap size  {0} px") },
            { "settings.hints_on", ("Подсказки управления  ·  вкл", "Input hints  ·  on") },
            { "settings.hints_off", ("Подсказки управления  ·  выкл", "Input hints  ·  off") },
            { "settings.keybinds", ("Клавиши", "Keybinds") },

            { "keys.title", ("Клавиши", "Controls") },
            { "keys.hint", ("Нажмите клавишу в списке, затем новую кнопку", "Click a key, then press the new button") },
            { "keys.reset_all", ("Сбросить всё", "Reset all") },
            { "keys.reset_title", ("Сбросить управление?", "Reset controls?") },
            { "keys.reset_body", ("Все клавиши вернутся к значениям по умолчанию.", "All keys will return to defaults.") },
            { "keys.reset", ("Сбросить", "Reset") },
            { "keys.movement", ("Движение", "Movement") },
            { "keys.build", ("Строительство", "Build") },
            { "keys.game", ("Игра", "Game") },
            { "keys.system", ("Система", "System") },

            { "bind.forward", ("Вперёд", "Move forward") },
            { "bind.back", ("Назад", "Move back") },
            { "bind.left", ("Влево", "Move left") },
            { "bind.right", ("Вправо", "Move right") },
            { "bind.place", ("Поставить", "Place") },
            { "bind.demolish", ("Снести", "Demolish") },
            { "bind.interact", ("Взаимодействие", "Interact") },
            { "bind.jump", ("Прыжок", "Jump") },
            { "bind.sprint", ("Бег", "Sprint") },
            { "bind.pause", ("Пауза", "Pause") },
            { "bind.rotate", ("Поворот", "Rotate") },
            { "bind.build_mode", ("Режим стройки", "Build mode") },
            { "bind.research", ("Исследования", "Research") },
            { "bind.select", ("Редактирование", "Edit mode") },
            { "bind.clear", ("Сбросить выделение", "Clear selection") },
            { "bind.copy", ("Копировать", "Copy") },
            { "bind.paste", ("Вставить", "Paste") },
            { "bind.map", ("Карта / перенос", "Map / move") },
            { "bind.modifier", ("Модификатор", "Modifier") },
            { "bind.delete", ("Удалить", "Delete") },
            { "bind.inventory", ("Инвентарь", "Inventory") },
            { "bind.shop", ("Магазин", "Shop") },
            { "bind.selected", ("Выделенные", "Selection") },

            { "overlay.build", ("Строительство", "Build") },
            { "overlay.research", ("Исследования", "Research") },
            { "overlay.shop", ("Магазин", "Shop") },
            { "overlay.selected", ("Выделенные здания", "Selected buildings") },
            { "overlay.map", ("Карта мира", "World map") },
            { "overlay.inventory", ("Инвентарь зданий", "Building inventory") },
            { "bag.hint", ("Перетащи в хотбар. ПКМ — убрать.", "Drag to the hotbar. RMB — remove.") },

            { "build.search", ("Поиск", "Search") },
            { "cat.all", ("Все", "All") },
            { "cat.logistics", ("Логистика", "Logistics") },
            { "cat.production", ("Производство", "Production") },
            { "cat.storage", ("Склады", "Storage") },
            { "cat.extraction", ("Добыча", "Extraction") },
            { "cat.research", ("Исследования", "Research") },
            { "cat.special", ("Особое", "Special") },

            { "research.pick", ("Выберите узел", "Select a node") },
            { "research.pick_body", ("Исследование открывает здания и рецепты.", "Research unlocks buildings and recipes.") },
            { "research.need", ("Требования", "Requirements") },
            { "research.unlocks", ("Открывает", "Unlocks") },
            { "research.start", ("Начать исследование", "Start research") },
            { "research.started", ("Исследование запущено", "Research started") },
            { "research.done", ("Готово", "Done") },
            { "research.active", ("Идёт", "Active") },
            { "research.ready", ("Доступно", "Ready") },
            { "research.locked", ("Закрыто", "Locked") },

            { "map.center", ("На игроке", "Center player") },
            { "map.forest", ("лес", "forest") },
            { "map.field", ("поле", "field") },
            { "map.mountain", ("гора", "mountain") },
            { "map.water", ("вода", "water") },
            { "map.veins", ("жилы", "veins") },
            { "map.buildings", ("здания", "buildings") },
            { "map.belts", ("ленты", "belts") },

            { "select.empty", ("Ничего не выделено. Режим редактирования → выдели здания → O.", "Nothing selected. Edit mode → select buildings → O.") },
            { "select.count", ("Выделено {0}   ·   групп {1}", "{0} buildings selected   ·   {1} groups") },
            { "select.upgrade", ("Прокачать {0}   ◈ {1}", "Upgrade {0}   ◈ {1}") },
            { "select.max", ("Макс. уровень", "Max level") },
            { "select.recipe", ("Сменить рецепт", "Change recipe") },
            { "select.dismantle", ("Delete — снести выделенное", "Delete — dismantle selected") },
            { "select.recipe_title", ("Рецепт", "Recipe") },
            { "select.choose", ("Выбрать", "Select") },

            { "shop.exchange", ("Обменять", "Exchange") },
            { "shop.coins", ("Монеты", "Coins") },
            { "shop.coins_tip", ("Основная валюта фабрики", "Factory currency") },
            { "shop.rubies", ("Рубины", "Rubies") },
            { "shop.rubies_tip", ("Премиум-валюта. Обмен в магазине", "Premium currency. Exchange in the shop") },
            { "shop.offer", ("{0}  →  {1} монет", "{0}  →  {1} coins") },
            { "shop.offer1", ("1 рубин  →  {0} монет", "1 ruby  →  {0} coins") },
            { "shop.offer5", ("5 рубинов  →  {0} монет", "5 rubies  →  {0} coins") },
            { "shop.none", ("Нет рубинов", "No rubies") },
            { "shop.all", ("Все {0}  →  {1} монет", "All {0}  →  {1} coins") },

            { "machine.upgrade", ("Прокачать", "Upgrade") },
            { "machine.upgrade_cost", ("Прокачать  ·  {0}", "Upgrade  ·  {0}") },
            { "machine.upgrade_ext", ("Сейчас: {0}   После: {1}", "Now: {0}   After: {1}") },
            { "machine.upgrade_craft", ("Скорость крафта {0}  →  ×2", "Craft speed {0}  →  ×2") },
            { "machine.upgrade_need", ("Не хватает монет", "Not enough coins") },
            { "machine.search_recipe", ("Поиск рецепта", "Search recipes") },
            { "machine.search_filter", ("Поиск фильтра", "Search filters") },
            { "machine.filter_any", ("Любые предметы", "Any items") },
            { "machine.filter_on", ("выбрано", "selected") },
            { "machine.filter_off", ("без фильтра", "no filter") },
            { "machine.filter_set", ("фильтр", "filter") },
            { "machine.filter_only", ("брать только это", "take only this") },

            { "load.loading", ("Загрузка…", "Loading…") },
            { "modal.cancel", ("Отмена", "Cancel") },
            { "modal.ok", ("ОК", "OK") },

            { "mouse.lmb", ("ЛКМ", "LMB") },
            { "mouse.rmb", ("ПКМ", "RMB") },
            { "mouse.wheel", ("колесо", "wheel") },
            { "hint.resume", ("продолжить", "resume") },
            { "hint.close_inv", ("закрыть инвентарь", "close inventory") },
            { "hint.free_slot", ("в свободный слот", "to a free slot") },
            { "hint.drag", ("перетащи", "drag") },
            { "hint.drag_bar", ("в хотбар / обратно", "to hotbar / back") },
            { "hint.remove_bar", ("убрать из хотбара", "remove from hotbar") },
            { "hint.close", ("закрыть", "close") },
            { "hint.close_shop", ("закрыть магазин", "close shop") },
            { "hint.close_sel", ("закрыть выделенные", "close selection") },
            { "hint.close_research", ("закрыть исследования", "close research") },
            { "hint.zoom", ("масштаб", "zoom") },
            { "hint.pan_map", ("двигать карту", "pan map") },
            { "hint.close_map", ("закрыть карту", "close map") },
            { "hint.build_mode", ("режим строительства", "build mode") },
            { "hint.map", ("карта", "map") },
            { "hint.shop", ("магазин", "shop") },
            { "hint.zoom_map", ("масштаб", "zoom") },
            { "hint.pause", ("пауза", "pause") },
            { "hint.confirm_paste", ("подтвердить вставку", "confirm paste") },
            { "hint.rotate_group", ("повернуть группу", "rotate group") },
            { "hint.rotate_in_place", ("повернуть на месте", "rotate in place") },
            { "hint.cancel", ("отмена", "cancel") },
            { "hint.look_away", ("взгляд в сторону", "look away") },
            { "hint.confirm_move", ("подтвердить перенос", "confirm move") },
            { "hint.release_place", ("отпусти {0}", "release {0}") },
            { "hint.place_line", ("поставить линию", "place line") },
            { "hint.select_cells", ("выделить клетки", "select cells") },
            { "hint.copy", ("копировать", "copy") },
            { "hint.move", ("переместить", "move") },
            { "hint.rotate_center", ("повернуть вокруг центра", "rotate around center") },
            { "hint.delete", ("удалить", "delete") },
            { "hint.sel_settings", ("настройки выделенных", "selection settings") },
            { "hint.clear_sel", ("сбросить выделение", "clear selection") },
            { "hint.paste", ("вставить", "paste") },
            { "hint.exit_edit", ("выйти из редактирования", "exit edit mode") },
            { "hint.exit_build", ("выйти из стройки", "exit build mode") },
            { "hint.inventory", ("инвентарь зданий", "building inventory") },
            { "hint.place", ("установить", "place") },
            { "hint.rotate", ("повернуть", "rotate") },
            { "hint.edit_mode", ("режим редактирования", "edit mode") },
            { "hint.demolish", ("снести", "demolish") },
            { "hint.hotbar", ("хотбар", "hotbar") },
        };
}
