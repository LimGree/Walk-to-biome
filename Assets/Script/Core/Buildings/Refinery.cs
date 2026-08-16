using System.Collections.Generic;
using UnityEngine;

public class Refinery : CrafterBuilding
{
    static readonly Vector2Int[] Cardinals =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    readonly List<Vector2Int> cells = new List<Vector2Int>(16);

    BuildingSocket pipeSocket;
    BuildingSocket beltSocket;

    void Awake()
    {
        CacheDefaultSockets();
    }

    public override void OnPlaced()
    {
        CacheDefaultSockets();
        ApplyPorts();
        base.OnPlaced();
    }

    public override void SetRecipe(RecipeData recipe)
    {
        base.SetRecipe(recipe);
        ApplyPorts();
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        ApplyPorts();
    }

    protected override bool AcceptsItem(ItemData item)
    {
        if (item == null)
            return false;
        if (currentRecipe == null)
            return item.isFluid;
        if (currentRecipe.inputs == null)
            return false;
        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            if (currentRecipe.inputs[i].item == item)
                return true;
        }
        return false;
    }

    protected override bool TryPushToConnections(ItemData item)
    {
        if (item == null)
            return false;

        if (base.TryPushToConnections(item))
            return true;

        if (item.isFluid || RecipeOutputsFluid(currentRecipe))
            return false;

        return TryPushToAdjacentBelts(item);
    }

    void CacheDefaultSockets()
    {
        if (pipeSocket == null && inputSockets != null && inputSockets.Length > 0)
            pipeSocket = inputSockets[0];
        if (beltSocket == null && outputSockets != null && outputSockets.Length > 0)
            beltSocket = outputSockets[0];
    }

    void ApplyPorts()
    {
        CacheDefaultSockets();
        if (pipeSocket == null || beltSocket == null)
            return;

        pipeSocket.DisconnectAll();
        beltSocket.DisconnectAll();

        if (RecipeOutputsFluid(currentRecipe))
        {
            beltSocket.socketType = SocketType.Input;
            pipeSocket.socketType = SocketType.Output;
            inputSockets = new[] { beltSocket };
            outputSockets = new[] { pipeSocket };
        }
        else
        {
            pipeSocket.socketType = SocketType.Input;
            beltSocket.socketType = SocketType.Output;
            inputSockets = new[] { pipeSocket };
            outputSockets = new[] { beltSocket };
        }

        BuildingLinker.RelinkAround(this);
    }

    static bool RecipeOutputsFluid(RecipeData recipe)
    {
        if (recipe == null || recipe.outputs == null)
            return false;
        for (int i = 0; i < recipe.outputs.Count; i++)
        {
            ItemData item = recipe.outputs[i].item;
            if (item != null && item.isFluid)
                return true;
        }
        return false;
    }

    bool TryPushToAdjacentBelts(ItemData item)
    {
        GridFootprint.CollectCells(transform.position, FootprintSize, cells);
        for (int i = 0; i < cells.Count; i++)
        {
            for (int d = 0; d < Cardinals.Length; d++)
            {
                BuildingBase other = BuildingLinker.GetBuildingAt(cells[i] + Cardinals[d]);
                Conveyor belt = other as Conveyor;
                if (belt == null || belt is Pipe)
                    continue;
                if (belt.TryAcceptTransfer(item, null))
                    return true;
            }
        }

        return false;
    }
}
