using UnityEngine;
using UnityEngine.EventSystems;

public class BagBuildingCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    BuildingData building;
    InventoryUI ui;
    bool dragged;

    public void Bind(InventoryUI owner, BuildingData data)
    {
        ui = owner;
        building = data;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragged || ui == null || eventData == null || eventData.dragging)
        {
            dragged = false;
            return;
        }
        ui.OnBagClicked(building, eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragged = true;
        if (ui != null)
            ui.BeginBagDrag(building, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ui != null)
            ui.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ui != null)
            ui.EndBagDrag(building, eventData);
        dragged = false;
    }
}
