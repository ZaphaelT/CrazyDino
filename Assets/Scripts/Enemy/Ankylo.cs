using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class Ankylo : EnemyDino
{
    [Header("Walka")]
    [SerializeField] private float attackRange = 2.5f;     
    [SerializeField] private float attackCooldown = 3.0f;  
    [SerializeField] private int attackDamage = 15;

    [Header("Agro")]
    [SerializeField] private float detectionRange = 15.0f;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private bool autoStartPatrol = true;

    private float _attackTimer = 0f;
    private int _currentPatrolIndex = 0;
    private Transform _playerTransform;

    [Networked] private bool IsWalking { get; set; }
    [Networked] private Vector3 NetworkedPosition { get; set; }
    [Networked] private Quaternion NetworkedRotation { get; set; }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }

    public override void Spawned()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();

        if (DinosaurController.Instance != null)
            _playerTransform = DinosaurController.Instance.transform;

        if (Object.HasStateAuthority)
        {
            if (maxHp <= 0) maxHp = 100;
            Hp = maxHp;

            if (autoStartPatrol && patrolPoints != null && patrolPoints.Length > 0)
            {
                _agent.isStopped = false;
                _agent.SetDestination(patrolPoints[0].position);
                IsWalking = true;
            }
        }
    }

    void Update()
    {
        if (!Object || !Object.IsValid) return;

        if (_playerTransform == null && DinosaurController.Instance != null)
            _playerTransform = DinosaurController.Instance.transform;

        if (_animator != null)
            _animator.SetBool("isWalking", IsWalking);

        if (Object.HasStateAuthority)
        {
            _attackTimer += Time.deltaTime;

            float distanceToPlayer = 999f;
            if (_playerTransform != null)
            {
                distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            }

            if (distanceToPlayer <= attackRange)
            {
                if (_agent != null) _agent.isStopped = true;
                IsWalking = false;

                RotateTowards(_playerTransform.position);

                if (_attackTimer >= attackCooldown)
                {
                    PerformAttack();
                }
            }
            else if (distanceToPlayer <= detectionRange)
            {
                if (_agent != null)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(_playerTransform.position);
                }
                IsWalking = true;
            }
            else
            {
                HandlePatrol();
            }

            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, NetworkedPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, NetworkedRotation, Time.deltaTime * 10f);
        }
    }

    private void PerformAttack()
    {
        _attackTimer = 0f;

        if (_playerTransform != null)
        {


            var dmg = _playerTransform.GetComponent<IDamageable>();

            if (dmg != null)
            {
                dmg.TakeDamage(attackDamage);
            }
        }

        RPC_PlayAttackAnimation();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnimation()
    {
        if (_animator != null) _animator.SetTrigger("Attack");
    }

    private void HandlePatrol()
    {
        if (_agent == null || patrolPoints == null || patrolPoints.Length == 0) return;

        if (_agent.isStopped) _agent.isStopped = false;

        if (!_agent.pathPending && _agent.remainingDistance <= 0.5f)
        {
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
            _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
        }

        IsWalking = _agent.velocity.sqrMagnitude > 0.1f;
    }

    private void RotateTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
        }
    }
}