using Fusion;
using UnityEngine;

public class DinosaurController : NetworkBehaviour, IDamageable
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
    [SerializeField] private GameObject uiCanvasRoot;

    private bool _isLocal;
    private bool _cameraDetached;
    private Vector3 _cameraOffset;
    private Quaternion _cameraRotationOnDetach;

    [Networked] private bool IsRunning { get; set; }
    [Networked] private bool IsAttacking { get; set; }
    [Networked] public float CurrentHealth { get; set; }

    private float _attackAnimDuration = 0.7f;
    private float _attackTimer = 0f;

    [SerializeField] private float attackRadius = 2.0f;
    [SerializeField] private LayerMask attackLayerMask;

    [Header("Stats to upgrade")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private int _hp = 10;
    [SerializeField] private float _speed;

    private float deadzone = 0.08f; // Dodaj pole

    private Vector3 _smoothedVelocity = Vector3.zero;
    private float smoothSpeed = 8f; // Mo¿esz ustawiæ przez [SerializeField]

    public override void Spawned()
    {
        _cc = GetComponent<NetworkCharacterController>();
        _animator = GetComponent<Animator>();

        if (Object.HasStateAuthority)
        {
            CurrentHealth = _hp;
        }

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
                _cameraOffset = playerCamera.transform.position - transform.position;
                _cameraRotationOnDetach = playerCamera.transform.rotation;
                playerCamera.transform.SetParent(null);
                _cameraDetached = true;
                playerCamera.gameObject.SetActive(true);
                var audio = playerCamera.GetComponent<AudioListener>();
                if (audio != null)
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
            if (uiCanvasRoot != null) uiCanvasRoot.SetActive(true);
        }
        else
        {
            if (uiCanvasRoot != null) uiCanvasRoot.SetActive(false);
        }
    }

    public override void Render()
    {
        if (_isLocal && PlayerDinoHP.Instance != null)
        {
            PlayerDinoHP.Instance.UpdatePlayerHealth(CurrentHealth, _hp);
        }
    }

    void LateUpdate()
    {
        if (_isLocal && playerCamera != null)
        {
            playerCamera.transform.position = transform.position + _cameraOffset;
            playerCamera.transform.rotation = _cameraRotationOnDetach;
        }

        if (_animator != null)
        {
            _animator.SetBool("isRunning", IsRunning);
            _animator.SetBool("isAttacking", IsAttacking);
        }
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.K))
        {
            TakeDamage(15);
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
            var inputDir = new Vector2(data.direction.x, data.direction.z);
            if (inputDir.sqrMagnitude > deadzone * deadzone)
            {
                var dirNormalized = inputDir.normalized;
                desiredVelocity = new Vector3(dirNormalized.x, 0, dirNormalized.y) * _speed;
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
            if (damageable != null && damageable != (IDamageable)this)
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (Object.HasStateAuthority)
        {
            CurrentHealth -= damage;
            if (CurrentHealth < 0) CurrentHealth = 0;

            if (CurrentHealth == 0)
            {
                if (Object.HasInputAuthority && GameEndScreenController.Instance != null)
                    GameEndScreenController.Instance.ShowLose();

                var operatorController = OperatorController.Instance;
                if (operatorController != null)
                    operatorController.RPC_ShowWinScreen();
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
        CurrentHealth = _hp;

        if (_cc != null)
            _cc.maxSpeed = _speed;
    }
}