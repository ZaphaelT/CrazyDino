using UnityEngine;

public class DinoAttackButton : MonoBehaviour
{
    public static DinosaurController LocalDino { get; set; }

    public void OnAttackButton()
    {
        if (LocalDino != null)
            LocalDino.Attack();
    }
}
