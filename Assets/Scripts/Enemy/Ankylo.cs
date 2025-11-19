using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Ankylo : EnemyDino
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

    [Header("Gracz i detekcja")]
    [SerializeField]
    private float playerDetectDistance = 10f;

    private int _currentPatrolIndex = 0;
    private Transform playerTransform;

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
            playerTransform = DinosaurController.Instance.transform;

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

        if (playerTransform == null && DinosaurController.Instance != null)
            playerTransform = DinosaurController.Instance.transform;

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

        if (_agent == null)
        {
            if (IsWalking)
                SetWalkingState(false);

            return;
        }

        bool chasingPlayer = false;
        if (playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= playerDetectDistance)
            {
                _agent.SetDestination(playerTransform.position);
                chasingPlayer = true;
            }
        }

        if (!chasingPlayer)
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                if (IsWalking)
                    SetWalkingState(false);

                return;
            }

            if (!_agent.hasPath && !_agent.pathPending)
            {
                _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
            }

            if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, acceptDistance))
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolPoints.Length;
                _agent.SetDestination(patrolPoints[_currentPatrolIndex].position);
            }
        }

        bool isMoving = !_agent.pathPending && _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, acceptDistance);

        if (IsWalking != isMoving)
            SetWalkingState(isMoving);
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
