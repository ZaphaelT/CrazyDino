using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDino : NetworkBehaviour, IDamageable
{
    public Animator _animator;
    public NavMeshAgent _agent;

    [Networked, OnChangedRender(nameof(OnIsDeadChanged))]
    public bool IsDead { get; set; }

    [Networked]
    public int Hp { get; set; }

    [SerializeField]
    public int maxHp = 100;

    [SerializeField]
    private int expValue = 25; // iloœæ expa za zabicie

    // Nowa wersja TakeDamage z atakuj¹cym
    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority || IsDead)
            return;

        Hp -= amount;

        if (Hp <= 0)
        {
            Hp = 0;
            IsDead = true;

            if (_agent != null)
                _agent.isStopped = true;

            GrantExpToKiller();
        }
        else
        {
            RPC_PlayTakeDamage();
        }
    }

    private void GrantExpToKiller()
    {
        if (DinoLevelingSystem.Instance != null)
        {
            DinoLevelingSystem.Instance.OnEnemyKilled(expValue);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTakeDamage()
    {
        if (_animator != null)
            _animator.SetTrigger("TakeDamage");
    }

    private void OnIsDeadChanged()
    {
        if (_animator != null)
            _animator.SetTrigger("Death");
    }

    public void OnDeathAnimationEnd()
    {
        if (Object.HasStateAuthority)
            Runner.Despawn(Object);
    }
}
