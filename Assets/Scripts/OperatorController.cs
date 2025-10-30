using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class OperatorController : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float panSpeed = 10f;
    [SerializeField] private float cameraHeight = 100f;
    [SerializeField] private float initialPitch = 75f;
    [SerializeField] private float deadzone = 0.08f;

    [Header("Movement")]
    [SerializeField] private bool useCameraRelativeMovement = false; // false = world-space
    [SerializeField] private float smoothSpeed = 8f;                  // wiêksza wartoœæ = p³ynniej szybciej

    [Header("Optional bounds (XZ)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    private InputSystem_Actions _controls;
    private bool _isLocal;
    private bool _cameraDetached;
    private bool _cameraIsRoot;

    // ostatni input kamery dostarczony przez BasicSpawner.OnInput
    private Vector2 _cameraInput = Vector2.zero;

    // debug helper: wymuœ lokalne sterowanie w edytorze (domyœlnie false)
    [SerializeField] private bool debugForceLocalInEditor = false;

    public override void Spawned()
    {
        _isLocal = Object.HasInputAuthority;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera == null)
            return;

        _cameraIsRoot = (playerCamera.gameObject == this.gameObject);

        if (_isLocal || (debugForceLocalInEditor && Application.isEditor))
        {
            if (!_cameraIsRoot)
            {
                playerCamera.transform.SetParent(null);
                _cameraDetached = true;
            }
            else
            {
                _cameraDetached = false;
            }

            playerCamera.transform.position = transform.position + Vector3.up * cameraHeight;
            playerCamera.transform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);
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

    private void OnEnable()
    {
        if (_controls == null)
            _controls = new InputSystem_Actions();

        _controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (_controls != null)
            _controls.Player.Disable();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            _cameraInput = data.camera;
        }
    }

    void LateUpdate()
    {
        bool localControl = Object.HasInputAuthority || (debugForceLocalInEditor && Application.isEditor);
        if (!localControl || playerCamera == null)
            return;

        Vector2 stick = _cameraInput;

        if (stick.sqrMagnitude > deadzone * deadzone)
        {
            Vector3 move;
            if (useCameraRelativeMovement)
            {
                Vector3 forward = playerCamera.transform.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                move = right * stick.x + forward * stick.y;
            }
            else
            {
                // world-space movement (X,Z)
                move = new Vector3(stick.x, 0f, stick.y);
            }

            Vector3 targetPos = playerCamera.transform.position + move * panSpeed * Time.deltaTime;
            targetPos.y = playerCamera.transform.position.y;

            if (useBounds)
            {
                targetPos.x = Mathf.Clamp(targetPos.x, minXZ.x, maxXZ.x);
                targetPos.z = Mathf.Clamp(targetPos.z, minXZ.y, maxXZ.y);
            }

            // p³ynne przejœcie
            float t = Mathf.Clamp01(smoothSpeed * Time.deltaTime);
            playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPos, t);
        }

        Vector3 lookTarget = new Vector3(playerCamera.transform.position.x, playerCamera.transform.position.y - 10f, playerCamera.transform.position.z);
        playerCamera.transform.rotation = Quaternion.Euler(initialPitch, playerCamera.transform.rotation.eulerAngles.y, 0f);
        playerCamera.transform.LookAt(lookTarget);
    }

    void OnDestroy()
    {
        if (_cameraDetached && playerCamera != null)
        {
            Destroy(playerCamera.gameObject);
        }
        else if (!_cameraDetached && playerCamera != null)
        {
            var audio = playerCamera.GetComponent<AudioListener>();
            if (audio != null)
                audio.enabled = false;
        }
    }
}
