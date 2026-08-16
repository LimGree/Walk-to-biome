using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class KeybindStore
{
    public const string KeyboardGroup = "Keyboard&Mouse";
    const string PrefsKey = "InputBindingOverrides";

    public struct Entry
    {
        public string actionName;
        public string label;
        public int bindingIndex;
    }

    static InputSystem_Actions reference;
    static bool sharedEnabled;
    static bool suspendedShared;
    static InputActionRebindingExtensions.RebindingOperation operation;
    static Action listenCallback;

    public static InputSystem_Actions Shared
    {
        get
        {
            EnsureShared();
            return reference;
        }
    }

    public static bool IsListening { get; private set; }
    public static bool BlocksGameplayInput => IsListening || Time.unscaledTime < ignoreInputUntil;
    public static event Action Changed;

    static float ignoreInputUntil;

    public static InputActionAsset ReferenceAsset
    {
        get
        {
            EnsureReference();
            return reference.asset;
        }
    }

    public static void Register(InputSystem_Actions actions)
    {
        EnsureShared();
    }

    public static void Register(InputActionAsset asset)
    {
        EnsureShared();
    }

    public static void Unregister(InputSystem_Actions actions)
    {
    }

    public static void Unregister(InputActionAsset asset)
    {
    }

    public static List<Entry> BuildEntries()
    {
        EnsureReference();
        var list = new List<Entry>(24);
        AddComposite(list, "Move", "up", "Вперёд");
        AddComposite(list, "Move", "down", "Назад");
        AddComposite(list, "Move", "left", "Влево");
        AddComposite(list, "Move", "right", "Вправо");
        AddKeyboard(list, "Place", "Поставить");
        AddKeyboard(list, "Demolish", "Снести");
        AddKeyboard(list, "Interact", "Взаимодействие");
        AddKeyboard(list, "Jump", "Прыжок");
        AddKeyboard(list, "Sprint", "Бег");
        AddKeyboard(list, "Pause", "Пауза");
        AddKeyboard(list, "Rotate", "Поворот");
        AddKeyboard(list, "BuildMode", "Режим стройки");
        AddKeyboard(list, "Research", "Исследования");
        AddKeyboard(list, "SelectMode", "Редактирование");
        AddKeyboard(list, "ClearSelection", "Сбросить выделение");
        AddKeyboard(list, "Copy", "Копировать");
        AddKeyboard(list, "Paste", "Вставить");
        AddKeyboard(list, "MoveSelection", "Карта / перенос");
        AddKeyboard(list, "Modifier", "Модификатор");
        AddKeyboard(list, "Delete", "Удалить");
        AddKeyboard(list, "Inventory", "Инвентарь");
        return list;
    }

    public static string Hint(string actionName)
    {
        EnsureReference();
        InputAction action = Find(actionName);
        int index = FirstKeyboardIndex(action);
        return Format(action, index);
    }

    public static string Format(Entry entry)
    {
        EnsureReference();
        return Format(Find(entry.actionName), entry.bindingIndex);
    }

    public static string EffectivePath(Entry entry)
    {
        EnsureReference();
        InputAction action = Find(entry.actionName);
        if (action == null || entry.bindingIndex < 0 || entry.bindingIndex >= action.bindings.Count)
            return "";
        return action.bindings[entry.bindingIndex].effectivePath ?? "";
    }

    public static void StartRebind(Entry entry, Action onEnded)
    {
        CancelListen();
        EnsureReference();
        InputAction action = Find(entry.actionName);
        if (action == null || entry.bindingIndex < 0 || entry.bindingIndex >= action.bindings.Count)
        {
            onEnded?.Invoke();
            return;
        }

        IsListening = true;
        listenCallback = onEnded;
        SuspendLive();

        operation = action.PerformInteractiveRebinding(entry.bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Gamepad>")
            .WithControlsExcluding("<Joystick>")
            .WithMatchingEventsBeingSuppressed(true)
            .OnMatchWaitForAnother(0.12f)
            .OnComplete(_ => FinishListen(true))
            .OnCancel(_ => FinishListen(false))
            .Start();
    }

    public static void CancelListen()
    {
        if (operation == null && !IsListening)
            return;
        FinishListen(false);
    }

    public static void ResetBinding(Entry entry)
    {
        EnsureReference();
        InputAction action = Find(entry.actionName);
        if (action == null || entry.bindingIndex < 0)
            return;
        action.RemoveBindingOverride(entry.bindingIndex);
        PersistFromReference();
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        CancelListen();
        EnsureReference();
        reference.asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static void FinishListen(bool applied)
    {
        if (operation != null)
        {
            operation.Dispose();
            operation = null;
        }

        IsListening = false;
        ignoreInputUntil = Time.unscaledTime + 0.2f;
        ResumeLive();

        if (applied)
            PersistFromReference();

        Changed?.Invoke();
        Action cb = listenCallback;
        listenCallback = null;
        cb?.Invoke();
    }

    static void PersistFromReference()
    {
        EnsureReference();
        string json = reference.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(PrefsKey, json ?? "");
        PlayerPrefs.Save();
    }

    static void ApplySaved(InputActionAsset asset)
    {
        if (asset == null)
            return;
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json))
            return;
        asset.LoadBindingOverridesFromJson(json);
    }

    static void EnsureReference()
    {
        EnsureShared();
    }

    static void EnsureShared()
    {
        if (reference == null || reference.asset == null)
        {
            reference = new InputSystem_Actions();
            ApplySaved(reference.asset);
        }

        if (!sharedEnabled && !IsListening)
        {
            reference.Enable();
            sharedEnabled = true;
        }
    }

    static InputAction Find(string actionName)
    {
        EnsureReference();
        return reference.asset.FindAction("Player/" + actionName, false);
    }

    static void AddComposite(List<Entry> list, string actionName, string part, string label)
    {
        InputAction action = Find(actionName);
        if (action == null)
            return;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isPartOfComposite)
                continue;
            if (!string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsKeyboardGroup(binding))
                continue;
            list.Add(new Entry { actionName = actionName, label = label, bindingIndex = i });
            return;
        }
    }

    static void AddKeyboard(List<Entry> list, string actionName, string label)
    {
        InputAction action = Find(actionName);
        if (action == null)
            return;
        int extra = 0;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;
            if (!IsKeyboardGroup(binding))
                continue;
            string name = extra == 0 ? label : label + " (доп.)";
            list.Add(new Entry { actionName = actionName, label = name, bindingIndex = i });
            extra++;
        }
    }

    static int FirstKeyboardIndex(InputAction action)
    {
        if (action == null)
            return -1;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;
            if (!IsKeyboardGroup(binding))
                continue;
            return i;
        }
        return -1;
    }

    static bool IsKeyboardGroup(InputBinding binding)
    {
        if (string.IsNullOrEmpty(binding.groups))
            return true;
        return binding.groups.IndexOf(KeyboardGroup, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void SuspendLive()
    {
        suspendedShared = false;
        if (reference == null || reference.asset == null)
            return;
        if (!reference.asset.enabled)
            return;
        reference.Disable();
        sharedEnabled = false;
        suspendedShared = true;
    }

    static void ResumeLive()
    {
        if (!suspendedShared || reference == null)
            return;
        reference.Enable();
        sharedEnabled = true;
        suspendedShared = false;
    }

    static string Format(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            return "—";

        string path = action.bindings[bindingIndex].effectivePath;
        if (string.IsNullOrEmpty(path))
            return "—";

        string lower = path.ToLowerInvariant();
        if (lower.Contains("leftbutton"))
            return "ЛКМ";
        if (lower.Contains("rightbutton"))
            return "ПКМ";
        if (lower.Contains("middlebutton"))
            return "СКМ";

        string display = action.GetBindingDisplayString(bindingIndex);
        return Localize(display);
    }

    static string Localize(string display)
    {
        if (string.IsNullOrEmpty(display))
            return "—";

        display = display.Replace(" / ", "/");
        switch (display.Trim())
        {
            case "Escape":
            case "Esc":
                return "Esc";
            case "Space":
            case "Spacebar":
                return "Пробел";
            case "Left Ctrl":
            case "Left Control":
            case "Ctrl":
                return "Левый Ctrl";
            case "Right Ctrl":
            case "Right Control":
                return "Правый Ctrl";
            case "Left Shift":
                return "Левый Shift";
            case "Right Shift":
                return "Правый Shift";
            case "Left Alt":
                return "Левый Alt";
            case "Right Alt":
                return "Правый Alt";
            case "Left Button":
            case "LMB":
                return "ЛКМ";
            case "Right Button":
            case "RMB":
                return "ПКМ";
            case "Middle Button":
            case "MMB":
                return "СКМ";
            case "Return":
                return "Enter";
            default:
                return display.Trim();
        }
    }
}
