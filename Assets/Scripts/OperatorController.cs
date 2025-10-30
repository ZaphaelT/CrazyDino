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

    [Header("Optional bounds (XZ)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    private InputSystem_Actions _controls; // mo¿esz zostawiæ jeœli u¿ywasz lokalnie te¿ UI
    private bool _isLocal;
    private bool _cameraDetached;

    // ostatni input kamery dostarczony przez BasicSpawner.OnInput
    private Vector2 _cameraInput = Vector2.zero;

    public override void Spawned()
    {
        _isLocal = Object.HasInputAuthority;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera != null)
        {
            if (_isLocal)
            {
                playerCamera.transform.SetParent(null);
                playerCamera.transform.position = transform.position + Vector3.up * cameraHeight;
                playerCamera.transform.rotation = Quaternion.Euler(initialPitch, 0f, 0f);

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

    // ODBIERAMY input z Fusion — uruchamia siê na tickach sieciowych.
    public override void FixedUpdateNetwork()
    {
        // GetInput zwróci dane tylko dla obiektu z InputAuthority (lokalny gracz)
        if (GetInput(out NetworkInputData data))
        {
            _cameraInput = data.camera;
        }
    }

    void LateUpdate()
    {
        // kontrolujemy kamerê tylko lokalnie (ma InputAuthority)
        if (!Object.HasInputAuthority || playerCamera == null)
            return;

        Vector2 stick = _cameraInput; // u¿ywamy wartoœci zapisanej w FixedUpdateNetwork

        if (stick.sqrMagnitude > deadzone * deadzone)
        {
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 move = right * stick.x + forward * stick.y;
            Vector3 newPos = playerCamera.transform.position + move * panSpeed * Time.deltaTime;

            newPos.y = playerCamera.transform.position.y;

            if (useBounds)
            {
                newPos.x = Mathf.Clamp(newPos.x, minXZ.x, maxXZ.x);
                newPos.z = Mathf.Clamp(newPos.z, minXZ.y, maxXZ.y);
            }

            playerCamera.transform.position = newPos;
        }

        Vector3 lookTarget = new Vector3(playerCamera.transform.position.x, playerCamera.transform.position.y - 10f, playerCamera.transform.position.z);
        playerCamera.transform.rotation = Quaternion.Euler(initialPitch, playerCamera.transform.rotation.eulerAngles.y, 0f);
        playerCamera.transform.LookAt(lookTarget);
    }

    void OnDestroy()
    {
        if (_cameraDetached && playerCamera != null)
            Destroy(playerCamera.gameObject);
    }
}
