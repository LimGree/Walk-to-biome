using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class KeybindSettingsUI
{
    public static void Fill(VisualElement parent, Action onBack)
    {
        if (parent == null)
            return;

        parent.Clear();
        parent.Add(IndustryUi.Text("T", "КЛАВИШИ", "title-hero"));
        parent.Add(IndustryUi.Text("H", "Нажмите клавишу в списке, затем новую кнопку", "muted"));
        var scroll = IndustryUi.Scroll("Keys");
        scroll.style.maxHeight = 420;
        parent.Add(scroll);

        var entries = KeybindStore.BuildEntries();
        var keyLabels = new List<Label>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            KeybindStore.Entry entry = entries[i];
            var row = IndustryUi.El("R", "card");
            row.Add(IndustryUi.Text("L", entry.label, "body-text", "grow"));
            Button key = IndustryUi.Btn(KeybindStore.Format(entry), null, "btn-small");
            Label lab = key.Q<Label>(className: "btn-label");
            keyLabels.Add(lab);
            key.clicked += () =>
            {
                if (lab != null)
                    lab.text = "...";
                KeybindStore.StartRebind(entry, () => Refresh(entries, keyLabels));
            };
            row.Add(key);
            Button reset = IndustryUi.Btn("↻", () =>
            {
                if (KeybindStore.IsListening)
                    return;
                KeybindStore.ResetBinding(entry);
                Refresh(entries, keyLabels);
            }, "btn-small");
            row.Add(reset);
            scroll.Add(row);
        }

        parent.Add(IndustryUi.Btn("Сбросить всё", () =>
        {
            KeybindStore.ResetAll();
            Refresh(entries, keyLabels);
        }));
        if (onBack != null)
            parent.Add(IndustryUi.Btn("Назад", onBack));
        Refresh(entries, keyLabels);
    }

    static void Refresh(List<KeybindStore.Entry> entries, List<Label> labels)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            string path = KeybindStore.EffectivePath(entries[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            counts.TryGetValue(path, out int n);
            counts[path] = n + 1;
        }

        for (int i = 0; i < labels.Count && i < entries.Count; i++)
        {
            if (labels[i] == null)
                continue;
            string path = KeybindStore.EffectivePath(entries[i]);
            bool conflict = !string.IsNullOrEmpty(path) && counts.TryGetValue(path, out int n) && n > 1;
            labels[i].text = KeybindStore.Format(entries[i]);
            labels[i].style.color = conflict
                ? new Color(0.86f, 0.59f, 0.27f)
                : new Color(0.91f, 0.65f, 0.29f);
        }
    }
}
