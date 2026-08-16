using UnityEngine;
using UnityEngine.EventSystems;

public class HotbarSlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int index;
    InventoryUI ui;

    public void Bind(InventoryUI owner, int slot)
    {
        ui = owner;
        index = slot;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ui != null)
            ui.OnHotbarClicked(index, eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ui != null)
            ui.BeginHotbarDrag(index, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ui != null)
            ui.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ui != null)
            ui.EndHotbarDrag(index, eventData);
    }
}
