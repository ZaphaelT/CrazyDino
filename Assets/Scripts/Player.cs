using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private NetworkCharacterController _cc;
    private Animator _animator;

    [SerializeField] private Camera playerCamera;
    private float _speed;

    private bool _isLocal;
    private bool _cameraDetached;
    private Vector3 _cameraOffset;
    private Quaternion _cameraRotationOnDetach;

    [Networked] private bool IsRunning { get; set; }

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
    }

    void LateUpdate()
    {
        if (_isLocal && playerCamera != null)
        {
            playerCamera.transform.position = transform.position + _cameraOffset;
            playerCamera.transform.rotation = _cameraRotationOnDetach;
        }

        // Ustaw animacjê na podstawie zsynchronizowanej zmiennej
        if (_animator != null)
            _animator.SetBool("isRunning", IsRunning);
    }

    void OnDestroy()
    {
        if (_cameraDetached && playerCamera != null)
            Destroy(playerCamera.gameObject);
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

        // Synchronizuj stan animacji przez sieæ
        IsRunning = isRunning;
    }
}