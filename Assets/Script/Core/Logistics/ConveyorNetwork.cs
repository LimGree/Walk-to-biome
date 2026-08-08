using UnityEngine;
using System.Collections.Generic;

public class ConveyorNetwork : MonoBehaviour
{
    // Пока просто хранит все ленты на сцене.
    // Позже сюда можно добавить поиск путей, оптимизацию и т.д.

    public static ConveyorNetwork Instance { get; private set; }

    private List<ConveyorBelt> allBelts = new List<ConveyorBelt>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterBelt(ConveyorBelt belt)
    {
        if (!allBelts.Contains(belt))
            allBelts.Add(belt);
    }

    public void UnregisterBelt(ConveyorBelt belt)
    {
        allBelts.Remove(belt);
    }

    // Пример будущего метода
    public List<ConveyorBelt> GetAllBelts() => allBelts;
}