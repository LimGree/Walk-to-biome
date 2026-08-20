using System;
using UnityEngine.UIElements;

public static class UiModal
{
    static VisualElement host;
    static Label title;
    static Label body;
    static Button confirm;
    static Action pending;

    public static void Confirm(string heading, string message, string confirmLabel, Action onConfirm)
    {
        Ensure();
        if (host == null)
            return;
        pending = onConfirm;
        title.text = heading ?? "";
        body.text = message ?? "";
        IndustryUi.SetButtonLabel(confirm, string.IsNullOrEmpty(confirmLabel) ? UiLocale.T("modal.ok") : confirmLabel);
        Button cancel = host != null ? host.Q<Button>("Cancel") : null;
        if (cancel != null)
            IndustryUi.SetButtonLabel(cancel, UiLocale.T("modal.cancel"));
        host.style.display = DisplayStyle.Flex;
        UiAudio.PlayOpen();
    }

    public static void Hide()
    {
        pending = null;
        if (host != null)
            host.style.display = DisplayStyle.None;
    }

    static void Ensure()
    {
        if (host != null && host.panel != null)
            return;
        VisualElement root = UiRuntime.HostRoot;
        if (root == null)
            return;
        host = IndustryUi.El("Modal", "modal-host", "screen");
        host.Add(IndustryUi.El("Dim", "dim"));
        var box = IndustryUi.El("Box", "modal-box");
        title = IndustryUi.Text("T", "", "heading-3");
        body = IndustryUi.Text("B", "", "muted");
        body.style.marginTop = 12;
        var actions = IndustryUi.El("A", "modal-actions");
        Button cancel = IndustryUi.Btn(UiLocale.T("modal.cancel"), Hide, "btn-small", "btn-ghost");
        cancel.name = "Cancel";
        actions.Add(cancel);
        confirm = IndustryUi.Btn(UiLocale.T("menu.delete"), () =>
        {
            Action act = pending;
            Hide();
            act?.Invoke();
        }, "btn-small", "btn-danger");
        actions.Add(confirm);
        box.Add(title);
        box.Add(body);
        box.Add(actions);
        host.Add(box);
        host.style.display = DisplayStyle.None;
        root.Add(host);
    }
}
