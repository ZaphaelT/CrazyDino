using Fusion;
using UnityEngine;

public class DinosaurController : NetworkBehaviour
{
    public static DinosaurController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private NetworkCharacterController _cc;
    private Animator _animator;

    [SerializeField] private Camera playerCamera;

    private bool _isLocal;
    private bool _cameraDetached;

    // przechowujemy offset i rotacjê wzglêdem lokalnego transform dino
    private Vector3 _cameraLocalOffset;
    private Quaternion _cameraLocalRotation;

    [Networked] private bool IsRunning { get; set; }
    [Networked] private bool IsAttacking { get; set; }
    private float _attackAnimDuration = 0.7f;
    private float _attackTimer = 0f;

    [SerializeField] private float attackRadius = 2.0f;
    [SerializeField] private LayerMask attackLayerMask;
    [Header("Stats to upgrade")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private int _hp = 10;
    [SerializeField] private float _speed;

    public override void Spawned()
    {
        _cc = GetComponent<NetworkCharacterController>();
        _animator = GetComponent<Animator>();

        if (_cc != null)
            _speed = _cc.maxSpeed;
        else
            _speed = 5f;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        _isLocal = Object.HasInputAuthority;

        if (playerCamera != null)
        {
            if (_isLocal)
            {
                // zapisz offset i rotacjê wzglêdem lokalnego transform dino
                _cameraLocalOffset = transform.InverseTransformPoint(playerCamera.transform.position);
                _cameraLocalRotation = Quaternion.Inverse(transform.rotation) * playerCamera.transform.rotation;

                // odczep kamerê, zachowaj œwiatow¹ pozycjê
                playerCamera.transform.SetParent(null, true);
                _cameraDetached = true;
                playerCamera.gameObject.SetActive(true);

                if (playerCamera.TryGetComponent<AudioListener>(out var audio))
                    audio.enabled = true;
            }
            else
            {
                playerCamera.gameObject.SetActive(false);
            }
        }

        if (_isLocal)
        {
            DinoAttackButton.LocalDino = this;
        }
    }

    void LateUpdate()
    {
        if (_isLocal && playerCamera != null)
        {
            // ustaw kamerê wg lokalnego offsetu wzglêdem dino (stabilne przy ró¿nych rotacjach spawn)
            playerCamera.transform.position = transform.TransformPoint(_cameraLocalOffset);
            playerCamera.transform.rotation = transform.rotation * _cameraLocalRotation;
        }

        if (_animator != null)
        {
            _animator.SetBool("isRunning", IsRunning);
            _animator.SetBool("isAttacking", IsAttacking);
        }
    }

    void OnDestroy()
    {
        if (_cameraDetached && playerCamera != null)
            Destroy(playerCamera.gameObject);

        if (_isLocal && DinoAttackButton.LocalDino == this)
            DinoAttackButton.LocalDino = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        Vector3 desiredVelocity = Vector3.zero;
        bool isRunning = false;

        if (GetInput(out NetworkInputData data))
        {
            var inputDir = data.direction;
            if (inputDir.sqrMagnitude > 0f)
            {
                var dirNormalized = inputDir.normalized;
                desiredVelocity = dirNormalized * _speed;
                isRunning = true;
            }
        }

        if (_cc != null)
            _cc.Move(desiredVelocity);
        else
            transform.position += desiredVelocity * Runner.DeltaTime;

        IsRunning = isRunning;

        if (IsAttacking)
        {
            _attackTimer += Runner.DeltaTime;
            if (_attackTimer >= _attackAnimDuration)
            {
                IsAttacking = false;
                _attackTimer = 0f;
            }
        }
    }

    public void Attack()
    {
        if (_isLocal && Object.HasInputAuthority)
        {
            RPC_Attack();
        }
    }

    private void PerformAttack()
    {
        Vector3 center = transform.position + transform.forward * (attackRadius * 0.5f);
        Collider[] hits = Physics.OverlapSphere(center, attackRadius, attackLayerMask);

        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Attack()
    {
        if (!IsAttacking)
        {
            IsAttacking = true;
            _attackTimer = 0f;
            PerformAttack();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = transform.position + transform.forward * (attackRadius * 0.5f);
        Gizmos.DrawWireSphere(center, attackRadius);
    }

    public void MultiplyStatsOnLevelUp(float multiplier)
    {
        _speed *= multiplier;
        attackDamage = Mathf.RoundToInt(attackDamage * multiplier);
        _hp = Mathf.RoundToInt(_hp * multiplier);

        if (_cc != null)
            _cc.maxSpeed = _speed;
    }
}