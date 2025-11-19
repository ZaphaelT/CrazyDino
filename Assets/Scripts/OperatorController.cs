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
    [SerializeField] private float smoothSpeed = 8f;                  // wiêksza wartoœæ = szybsze wyg³adzanie

    [Header("Optional bounds (XZ)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    private InputSystem_Actions _controls;
    private bool _isLocal;
    private bool _cameraDetached;
    private bool _cameraIsRoot;

    // cache transform kamery dla wydajnoœci
    private Transform _cameraTransform;

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

        _cameraTransform = playerCamera.transform;
        _cameraIsRoot = (playerCamera.gameObject == this.gameObject);

        if (_isLocal || (debugForceLocalInEditor && Application.isEditor))
        {
            if (!_cameraIsRoot)
            {
                // zachowaj pozycjê œwiata przy odczepianiu
                _cameraTransform.SetParent(null, true);
                _cameraDetached = true;
            }
            else
            {
                _cameraDetached = false;
            }

            _cameraTransform.position = transform.position + Vector3.up * cameraHeight;
            _cameraTransform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);
            playerCamera.gameObject.SetActive(true);

            if (playerCamera.TryGetComponent<AudioListener>(out var audio))
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

        // u¿ywamy zcache'owanej transformaty jeœli dostêpna
        Transform camT = _cameraTransform != null ? _cameraTransform : playerCamera.transform;

        Vector2 stick = _cameraInput;

        if (stick.sqrMagnitude > deadzone * deadzone)
        {
            Vector3 move;
            if (useCameraRelativeMovement)
            {
                Vector3 forward = camT.forward;
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

            // oblicz prêdkoœæ (jednostki na sekundê)
            Vector3 velocity = move * panSpeed;

            // docelowa pozycja w tym kroku (bez dodatkowego skalowania wyg³adzaj¹cego)
            Vector3 stepTarget = camT.position + velocity * Time.deltaTime;
            stepTarget.y = camT.position.y;

            // stabilne wyg³adzanie niezale¿ne od FPS:
            // alpha = 1 - exp(-k * dt), gdzie k = smoothSpeed
            float alpha = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            camT.position = Vector3.Lerp(camT.position, stepTarget, alpha);

            // natychmiastowe przyciêcie finalnej pozycji
            if (useBounds)
            {
                camT.position = new Vector3(
                    Mathf.Clamp(camT.position.x, minXZ.x, maxXZ.x),
                    camT.position.y,
                    Mathf.Clamp(camT.position.z, minXZ.y, maxXZ.y)
                );
            }
        }

        // Utrzymuj sta³y pitch (initialPitch) i bie¿¹cy yaw
        float yaw = camT.rotation.eulerAngles.y;
        camT.rotation = Quaternion.Euler(initialPitch, yaw, 0f);
    }

    void OnDestroy()
    {
        if (playerCamera == null)
            return;

        if (_cameraDetached)
        {
            Destroy(playerCamera.gameObject);
        }
        else
        {
            if (playerCamera.TryGetComponent<AudioListener>(out var audio))
                audio.enabled = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;

        // kolor i gruboœæ
        Gizmos.color = Color.cyan;

        Vector3 center = new Vector3((minXZ.x + maxXZ.x) * 0.5f, cameraHeight, (minXZ.y + maxXZ.y) * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(maxXZ.x - minXZ.x), 0.1f, Mathf.Abs(maxXZ.y - minXZ.y));

        Gizmos.DrawWireCube(center, size);
    }
#endif
}
