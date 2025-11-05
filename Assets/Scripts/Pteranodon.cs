using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Pteranodon : NetworkBehaviour, IDamageable
{
    private Animator _animator;
    private NavMeshAgent _agent;

    [Networked, OnChangedRender(nameof(OnIsWalkingChanged))]
    private bool IsWalking { get; set; }

    [Networked, OnChangedRender(nameof(OnIsDeadChanged))]
    private bool IsDead { get; set; }

    [Networked]
    private int Hp { get; set; }

    [Networked]
    private Vector3 NetworkedPosition { get; set; }

    [Networked]
    private Quaternion NetworkedRotation { get; set; }

    [SerializeField]
    private int maxHp = 100;

    [Header("Patrol")]
    [SerializeField]
    private Transform[] patrolPoints = new Transform[0];

    [SerializeField]
    private float acceptDistance = 0.5f;

    [SerializeField]
    private bool autoStartPatrol = true;

    private int _currentPatrolIndex = 0;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
    }

    public override void Spawned()
    {
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();

        if (Object.HasStateAuthority)
        {
            Hp = maxHp;

            if (autoStartPatrol && patrolPoints != null && patrolPoints.Length > 0 && _agent != null)
            {
                _agent.isStopped = false;
                _currentPatrolIndex = 0;
                _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
                IsWalking = true;
            }
        }
    }

    void Update()
    {
        if (!Object || !Object.IsValid)
            return;

        if (Object.HasStateAuthority)
        {
            if (IsDead)
            {
                if (_agent != null && !_agent.isStopped)
                {
                    _agent.isStopped = true;
                }

                if (IsWalking)
                    IsWalking = false;

                return;
            }

            if (_agent == null || patrolPoints == null || patrolPoints.Length == 0)
            {
                if (IsWalking)
                    IsWalking = false;

                return;
            }

            if (!_agent.hasPath && !_agent.pathPending)
            {
                _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
            }

            bool isMoving = !_agent.pathPending && _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, acceptDistance);

            if (IsWalking != isMoving)
                IsWalking = isMoving;

            if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, acceptDistance))
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
                _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
            }

            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
        }
        else
        {
            transform.position = NetworkedPosition;
            transform.rotation = NetworkedRotation;
        }
    }

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
        }
        else
        {
            RPC_PlayTakeDamage();

        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTakeDamage()
    {
        if (_animator != null)
            _animator.SetTrigger("TakeDamage");
    }

    private void OnIsWalkingChanged()
    {
        if (_animator != null)
            _animator.SetBool("isWalking", IsWalking);
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
