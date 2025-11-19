using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDino : NetworkBehaviour, IDamageable
{
    protected Animator _animator;
    [SerializeField] protected NavMeshAgent _agent;

    [Networked, OnChangedRender(nameof(OnIsDeadChanged))]
    protected bool IsDead { get; set; }

    [Networked] protected int Hp { get; set; }

    [SerializeField] protected int maxHp = 100;

    [SerializeField] private int expValue = 25; 

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
