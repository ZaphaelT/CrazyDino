using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Pteranodon : EnemyDino
{

    private bool IsWalking = false;


    [Networked]
    private Vector3 NetworkedPosition { get; set; }

    [Networked]
    private Quaternion NetworkedRotation { get; set; }

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
                SetWalkingState(true);
            }
        }
    }

    void Update()
    {
        if (!Object || !Object.IsValid)
            return;

        if (Object.HasStateAuthority)
        {
            HandlePatrolAndMovement();
            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
        }
        else
        {
            transform.position = NetworkedPosition;
            transform.rotation = NetworkedRotation;
        }
    }

    private void HandlePatrolAndMovement()
    {
        if (IsDead)
        {
            if (_agent != null && !_agent.isStopped)
            {
                _agent.isStopped = true;
            }

            if (IsWalking)
                SetWalkingState(false);

            return;
        }

        if (_agent == null || patrolPoints == null || patrolPoints.Length == 0)
        {
            if (IsWalking)
                SetWalkingState(false);

            return;
        }

        if (!_agent.hasPath && !_agent.pathPending)
        {
            _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
        }

        bool isMoving = !_agent.pathPending && _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, acceptDistance);

        if (IsWalking != isMoving)
            SetWalkingState(isMoving);

        if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, acceptDistance))
        {
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
            _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
        }
    }

    private void SetWalkingState(bool walking)
    {
        IsWalking = walking;
        RPC_SetWalkingState(walking);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetWalkingState(bool walking)
    {
        if (_animator != null)
            _animator.SetBool("isWalking", walking);
    }


}
