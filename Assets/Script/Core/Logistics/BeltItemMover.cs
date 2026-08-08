using UnityEngine;

// Этот скрипт можно вообще не вешать. 
// Оставлен на случай, если захочешь вынести логику движения отдельно.
public class BeltItemMover : MonoBehaviour
{
    public ConveyorBelt belt;

    void Update()
    {
        // Логика движения сейчас находится прямо в ConveyorBelt
    }
}