using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // Potrzebne do obs³ugi przycisków

public class OperatorController : NetworkBehaviour
{
    public static OperatorController Instance { get; private set; }

    // --- ORYGINALNE USTAWIENIA KAMERY ---
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float panSpeed = 10f;
    [SerializeField] private float cameraHeight = 100f;
    [SerializeField] private float initialPitch = 75f;
    [SerializeField] private float deadzone = 0.08f;

    [Header("Movement")]
    [SerializeField] private bool useCameraRelativeMovement = false; // false = world-space
    [SerializeField] private float smoothSpeed = 8f;                 // wiêksza wartoœæ = szybsze wyg³adzanie

    [Header("Optional bounds (XZ)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    // --- NOWE USTAWIENIA DRONA I UI ---
    [Header("Drone System")]
    [SerializeField] private NetworkPrefabRef dronePrefab;
    [SerializeField] private Transform droneSpawnPoint;
    [SerializeField] private LayerMask groundLayer;

    [Header("Mobile UI")]
    [SerializeField] private GameObject uiCanvasRoot;
    [SerializeField] private Button spawnDroneButton;
    [SerializeField] private Button moveDroneButton;
    [SerializeField] private Button dropBombButton;

    // --- ZMIENNE WEWNÊTRZNE ---
    private InputSystem_Actions _controls;
    private bool _isLocal;
    private bool _cameraDetached;
    private bool _cameraIsRoot;
    private Transform _cameraTransform;
    private Vector2 _cameraInput = Vector2.zero;

    // To jest kluczowa zmienna - referencja do drona, którym sterujemy
    private DroneController _controlledDrone;

    [SerializeField] private bool debugForceLocalInEditor = false;

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        _isLocal = Object.HasInputAuthority;

        // --- SETUP KAMERY (Oryginalny + poprawki) ---
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera == null)
            return;

        _cameraTransform = playerCamera.transform;
        _cameraIsRoot = (playerCamera.gameObject == this.gameObject);

        // Konfiguracja dla lokalnego gracza (Operatora)
        if (_isLocal || (debugForceLocalInEditor && Application.isEditor))
        {
            if (!_cameraIsRoot)
            {
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

            // W³¹czamy UI
            if (uiCanvasRoot != null) uiCanvasRoot.SetActive(true);

            // Podpinamy przyciski
            if (spawnDroneButton) spawnDroneButton.onClick.AddListener(OnSpawnClick);
            if (moveDroneButton) moveDroneButton.onClick.AddListener(OnMoveClick);
            if (dropBombButton) dropBombButton.onClick.AddListener(OnBombClick);
        }
        else
        {
            // Wy³¹czamy wszystko dla innych graczy
            playerCamera.gameObject.SetActive(false);
            if (uiCanvasRoot != null) uiCanvasRoot.SetActive(false);
        }

        // --- SZUKANIE BAZY DRONA (FIX DLA PREFABU) ---
        if (droneSpawnPoint == null)
        {
            var found = GameObject.FindGameObjectWithTag("DroneBase");
            if (found != null)
            {
                droneSpawnPoint = found.transform;
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

    // --- ZARZ¥DZANIE STANEM UI ---
    void Update()
    {
        if (!_isLocal) return;

        bool hasDrone = _controlledDrone != null && _controlledDrone.Object != null && _controlledDrone.Object.IsValid;

        if (spawnDroneButton) spawnDroneButton.interactable = !hasDrone;
        if (moveDroneButton) moveDroneButton.interactable = hasDrone;
        if (dropBombButton) dropBombButton.interactable = hasDrone && _controlledDrone.IsBombReady;
    }

    // --- INPUT SIECIOWY ---
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            _cameraInput = data.camera;
        }
    }

    // --- PE£NA LOGIKA RUCHU KAMERY (TWÓJ ORYGINALNY KOD) ---
    void LateUpdate()
    {
        bool localControl = Object.HasInputAuthority || (debugForceLocalInEditor && Application.isEditor);
        if (!localControl || playerCamera == null)
            return;

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

            // docelowa pozycja w tym kroku
            Vector3 stepTarget = camT.position + velocity * Time.deltaTime;
            stepTarget.y = camT.position.y;

            // stabilne wyg³adzanie niezale¿ne od FPS
            float alpha = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            camT.position = Vector3.Lerp(camT.position, stepTarget, alpha);

            // przyciêcie pozycji (bounds)
            if (useBounds)
            {
                camT.position = new Vector3(
                    Mathf.Clamp(camT.position.x, minXZ.x, maxXZ.x),
                    camT.position.y,
                    Mathf.Clamp(camT.position.z, minXZ.y, maxXZ.y)
                );
            }
        }

        // Utrzymuj sta³y pitch i bie¿¹cy yaw
        float yaw = camT.rotation.eulerAngles.y;
        camT.rotation = Quaternion.Euler(initialPitch, yaw, 0f);
    }

    // --- OBS£UGA UI (KLIKNIÊCIA) ---

    private void OnSpawnClick()
    {
        RPC_RequestSpawnDrone();
    }

    private void OnMoveClick()
    {
        if (_controlledDrone == null || playerCamera == null) return;

        // Raycast ze œrodka ekranu
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            RPC_OrderMove(hit.point);
        }
    }

    private void OnBombClick()
    {
        RPC_OrderDropBomb();
    }

    // --- LOGIKA SIECIOWA (RPCS) ---

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnDrone(RpcInfo info = default)
    {
        // Sprawdzenie czy dron ju¿ istnieje
        if (_controlledDrone != null && _controlledDrone.Object != null && _controlledDrone.Object.IsValid)
            return;

        if (droneSpawnPoint == null)
        {
            Debug.LogError("OperatorController: Nie znaleziono DroneSpawnPoint (SprawdŸ Tag 'DroneBase')");
            return;
        }

        // Spawn drona
        NetworkObject droneObj = Runner.Spawn(dronePrefab, droneSpawnPoint.position, Quaternion.identity, info.Source);
        DroneController droneScript = droneObj.GetComponent<DroneController>();

        // 1. Zapisujemy drona u Siebie (na Serwerze) - TO BY£O KLUCZOWE
        _controlledDrone = droneScript;

        // 2. Wysy³amy informacjê do Klienta, ¿eby te¿ zapisa³ drona
        RPC_SetLocalDroneRef(droneScript);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SetLocalDroneRef(DroneController drone)
    {
        // To wykonuje siê u Klienta (Operatora)
        _controlledDrone = drone;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_OrderMove(Vector3 target)
    {
        // Serwer otrzymuje rozkaz i przekazuje go dronowi
        if (_controlledDrone != null)
        {
            _controlledDrone.Server_MoveTo(target);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_OrderDropBomb()
    {
        // Serwer otrzymuje rozkaz zrzutu
        if (_controlledDrone != null)
        {
            _controlledDrone.Server_DropBomb();
        }
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
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_ShowWinScreen()
    {
        if (GameEndScreenController.Instance != null)
            GameEndScreenController.Instance.ShowWin();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;

        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((minXZ.x + maxXZ.x) * 0.5f, cameraHeight, (minXZ.y + maxXZ.y) * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(maxXZ.x - minXZ.x), 0.1f, Mathf.Abs(maxXZ.y - minXZ.y));

        Gizmos.DrawWireCube(center, size);
    }
#endif
}