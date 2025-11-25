using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDino : NetworkBehaviour, IDamageable
{
    protected Animator _animator;
    [SerializeField] protected NavMeshAgent _agent;
    [SerializeField] private Collider _collider;

    [Networked] public bool IsDead { get; set; }
    [Networked] protected int Hp { get; set; }

    [SerializeField] protected int maxHp = 100;
    [SerializeField] private int expValue = 25;

    private bool _lastDeadState = false;

    public override void Spawned()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
        if (_collider == null) _collider = GetComponent<Collider>();

        _lastDeadState = IsDead;

        // Ustaw stan pocz¹tkowy
        if (IsDead)
        {
            SetVisualsActive(false);
            if (_agent != null) _agent.enabled = false;
            if (_collider != null) _collider.enabled = false;
        }
        else
        {
            SetVisualsActive(true);
            if (Object.HasStateAuthority) Hp = maxHp;
        }
    }

    public override void Render()
    {
        if (IsDead != _lastDeadState)
        {
            if (IsDead)
            {
                // ŒMIERÆ:
                // 1. Najpierw wy³¹czamy agenta, ¿eby Ankylo przesta³ nim sterowaæ
                if (_agent != null) _agent.enabled = false;

                // 2. Wy³¹czamy kolizje
                if (_collider != null) _collider.enabled = false;

                // 3. Animacja
                if (_animator != null && _animator.gameObject.activeInHierarchy)
                    _animator.SetTrigger("Death");
            }
            // (Respawn wizualny obs³u¿ony w Respawn())

            _lastDeadState = IsDead;
        }
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;

        Hp = maxHp;

        // ODRODZENIE (Kolejnoœæ krytyczna dla NavMesha!)
        if (_agent != null)
        {
            _agent.enabled = true; // 1. W³¹czamy komponent
            _agent.Warp(position); // 2. Przenosimy na NavMesh

            // 3. Resetujemy parametry TYLKO jeœli agent jest aktywny i na siatce
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.ResetPath();
            }
        }
        else
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        if (_collider != null) _collider.enabled = true;

        IsDead = false; // To zsynchronizuje klientów

        // Wymuszamy grafikê od razu na serwerze
        SetVisualsActive(true);
        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    public void OnDeathAnimationEnd()
    {
        SetVisualsActive(false);
    }

    private void SetVisualsActive(bool isActive)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(isActive);
        }
    }

    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority || IsDead) return;

        Hp -= amount;

        if (Hp <= 0)
        {
            Hp = 0;
            IsDead = true;
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
            DinoLevelingSystem.Instance.OnEnemyKilled(expValue);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTakeDamage()
    {
        if (_animator != null && _animator.gameObject.activeInHierarchy)
            _animator.SetTrigger("TakeDamage");
    }
}