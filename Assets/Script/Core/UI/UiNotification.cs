using UnityEngine;
using UnityEngine.UIElements;

public static class UiNotification
{
    const int LifeMs = 3200;

    static VisualElement host;

    public static void Push(string heading, string detail, UiStatus status = UiStatus.Neutral)
    {
        Ensure();
        if (host == null)
            return;
        var toast = IndustryUi.El("Toast", "toast");
        if (status == UiStatus.Completed || status == UiStatus.Ready)
            toast.AddToClassList("toast-ok");
        else if (status == UiStatus.Warning)
            toast.AddToClassList("toast-warn");
        else if (status == UiStatus.Error)
            toast.AddToClassList("toast-error");
        toast.Add(IndustryUi.Text("T", heading ?? "", "toast-title"));
        if (!string.IsNullOrEmpty(detail))
            toast.Add(IndustryUi.Text("B", detail, "toast-body"));
        host.Insert(0, toast);
        toast.schedule.Execute(() =>
        {
            toast.RemoveFromHierarchy();
        }).StartingIn(LifeMs);
        while (host.childCount > 4)
            host.RemoveAt(host.childCount - 1);
    }

    static void Ensure()
    {
        if (host != null && host.panel != null)
            return;
        VisualElement root = UiRuntime.HostRoot;
        if (root == null)
            return;
        host = IndustryUi.El("Toasts", "toast-host");
        host.pickingMode = PickingMode.Ignore;
        root.Add(host);
    }
}
