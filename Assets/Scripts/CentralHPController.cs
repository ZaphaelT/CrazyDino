using Fusion;
using UnityEngine;

public class CentralHPController : NetworkBehaviour, IDamageable
{
    [Header("Ustawienia HP centrali")]
    [SerializeField] private int maxHP = 100;
    [Networked] public int CurrentHP { get; set; }

    private void Awake()
    {
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            CurrentHP = maxHP;
    }

    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHP -= amount;
        if (CurrentHP < 0) CurrentHP = 0;

        // Przegrana operatora, wygrana dinozaura
        if (CurrentHP == 0)
        {
    
            var dinoController = DinosaurController.Instance;
            if (dinoController != null)
                dinoController.RPC_ShowWinScreen();

            var operatorController = OperatorController.Instance;
            if (operatorController != null)
                operatorController.RPC_ShowLoseScreen();
        }
    }
}