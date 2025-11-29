using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDino : NetworkBehaviour, IDamageable
{
    protected Animator _animator;
    [SerializeField] protected NavMeshAgent _agent;
    [SerializeField] private BoxCollider _collider;

    // --- NOWE POLA AUDIO ---
    [Header("Audio")]
    public AudioSource _audioSource; // Komponent AudioSource (3D)
    [SerializeField] private AudioClip takeDamageSound;       // DŸwiêk otrzymania obra¿eñ
    // -----------------------

    [Networked] public bool IsDead { get; set; }
    [Networked] protected int Hp { get; set; }

    [SerializeField] protected int maxHp = 100;
    [SerializeField] private int expValue = 25;

    private bool _lastDeadState = false;

    public override void Spawned()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
        if (_collider == null) _collider = GetComponent<BoxCollider>();

        // Automatyczne pobranie AudioSource, jeœli zapomnia³eœ przypisaæ
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        _lastDeadState = IsDead;

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
                if (_agent != null) _agent.enabled = false;
                if (_collider != null) _collider.enabled = false;
                if (_animator != null && _animator.gameObject.activeInHierarchy)
                    _animator.SetTrigger("Death");
            }
            else
            {
                SetVisualsActive(true);
                // Respawn logikê obs³uguje metoda Respawn(), tutaj tylko wizualne w³¹czenie
                if (_collider != null) _collider.enabled = true;
                if (_animator != null)
                {
                    _animator.Rebind();
                    _animator.Update(0f);
                }
            }
            _lastDeadState = IsDead;
        }
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;

        Hp = maxHp;

        if (_agent != null)
        {
            _agent.enabled = true;
            _agent.Warp(position);

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

        IsDead = false;

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
            // Jeœli ¿yje, ale oberwa³ -> odegraj animacjê i dŸwiêk
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
        // 1. Animacja
        if (_animator != null && _animator.gameObject.activeInHierarchy)
            _animator.SetTrigger("TakeDamage");

        // 2. DŸwiêk (NOWE)
        if (_audioSource != null && takeDamageSound != null)
        {
            // Losowa zmiana tonacji, ¿eby ka¿de uderzenie brzmia³o nieco inaczej
            _audioSource.pitch = Random.Range(0.9f, 1.1f);
            _audioSource.PlayOneShot(takeDamageSound);
        }
    }
}