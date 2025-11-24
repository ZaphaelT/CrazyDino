using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI; // Dodane do obs³ugi przycisków mobilnych

public class OperatorController : NetworkBehaviour
{
    // --- TWOJE ISTNIEJ¥CE ZMIENNE KAMERY ---
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float panSpeed = 10f;
    [SerializeField] private float cameraHeight = 100f;
    [SerializeField] private float initialPitch = 75f;
    [SerializeField] private float deadzone = 0.08f;

    [Header("Movement")]
    [SerializeField] private bool useCameraRelativeMovement = false;
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Optional bounds (XZ)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    // --- NOWE ZMIENNE DLA DRONA I UI ---
    [Header("Drone System")]
    [SerializeField] private NetworkPrefabRef dronePrefab;
    [SerializeField] private Transform droneSpawnPoint; // Punkt w bazie/fortecy
    [SerializeField] private LayerMask groundLayer;     // Warstwa pod³ogi (do klikania "idŸ tam")

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

    // Referencja do naszego drona (¿eby wiedzieæ czym sterowaæ)
    private DroneController _myDrone;

    [SerializeField] private bool debugForceLocalInEditor = false;

    public override void Spawned()
    {
        // --- TWOJA LOGIKA KAMERY ---
        _isLocal = Object.HasInputAuthority;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera == null) return;

        _cameraTransform = playerCamera.transform;
        _cameraIsRoot = (playerCamera.gameObject == this.gameObject);

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

            if (uiCanvasRoot != null) uiCanvasRoot.SetActive(true);
            // --- NOWA LOGIKA: PODPIÊCIE PRZYCISKÓW UI ---
            // Podpinamy funkcje tylko jeœli to nasz lokalny gracz
            if (spawnDroneButton) spawnDroneButton.onClick.AddListener(OnSpawnClick);
            if (moveDroneButton) moveDroneButton.onClick.AddListener(OnMoveClick);
            if (dropBombButton) dropBombButton.onClick.AddListener(OnBombClick);
        }
        else
        {
            playerCamera.gameObject.SetActive(false);
            if (uiCanvasRoot != null) uiCanvasRoot.SetActive(false);

            // Ukrywamy UI dla gracza, który nie jest operatorem (np. dla Dinozaura)
            if (spawnDroneButton) spawnDroneButton.gameObject.SetActive(false);
            if (moveDroneButton) moveDroneButton.gameObject.SetActive(false);
            if (dropBombButton) dropBombButton.gameObject.SetActive(false);
        }

        if (droneSpawnPoint == null)
        {
            GameObject foundBase = GameObject.FindGameObjectWithTag("DroneBase");
            if (foundBase != null)
            {
                droneSpawnPoint = foundBase.transform;
            }
            else
            {
                // Tylko serwer musi to wiedzieæ, ¿eby zespawnowaæ drona, ale warto logowaæ b³¹d
                if (Object.HasStateAuthority)
                {
                    Debug.LogError("B£¥D: Nie znaleziono obiektu z tagiem 'DroneBase' na scenie!");
                }
            }
        }
    }

    // --- NOWA METODA UPDATE (DLA UI) ---
    void Update()
    {
        // Tylko lokalny gracz zarz¹dza swoim UI
        if (!_isLocal && !(debugForceLocalInEditor && Application.isEditor)) return;

        // Sprawdzamy czy mamy drona (obiekt istnieje i nie zosta³ zniszczony)
        bool hasDrone = _myDrone != null && _myDrone.Object != null && _myDrone.Object.IsValid;

        // Zarz¹dzanie aktywnoœci¹ przycisków
        if (spawnDroneButton) spawnDroneButton.interactable = !hasDrone; // Mo¿na spawnowaæ tylko jak NIE ma drona
        if (moveDroneButton) moveDroneButton.interactable = hasDrone;    // Mo¿na ruszaæ tylko jak JEST dron

        if (dropBombButton)
        {
            // Mo¿na zrzuciæ bombê jak jest dron I cooldown min¹³
            dropBombButton.interactable = hasDrone && _myDrone.IsBombReady;
        }
    }

    // --- OBS£UGA PRZYCISKÓW (FUNKCJE LOKALNE) ---

    private void OnSpawnClick()
    {
        RPC_RequestSpawnDrone();
    }

    private void OnMoveClick()
    {
        if (_myDrone == null || playerCamera == null) return;

        // Strzelamy promieniem ze œrodka ekranu (celownika kamery)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // Sprawdzamy czy trafiliœmy w pod³ogê (groundLayer)
        if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
        {
            // Jeœli tak, wysy³amy rozkaz do serwera
            RPC_OrderMove(hit.point);
        }
    }

    private void OnBombClick()
    {
        if (_myDrone != null)
        {
            _myDrone.TryDropBomb();
        }
    }

    // --- LOGIKA SIECIOWA (RPC) ---

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnDrone(RpcInfo info = default)
    {
        // ZABEZPIECZENIE: Sprawdzamy czy punkt startu zosta³ znaleziony
        if (droneSpawnPoint == null)
        {
            Debug.LogError("B£¥D KRYTYCZNY: Serwer nie wie gdzie zespawnowaæ drona! droneSpawnPoint jest null.");

            // Próba ratunkowa - szukamy jeszcze raz na serwerze
            var found = GameObject.FindGameObjectWithTag("DroneBase");
            if (found != null)
            {
                droneSpawnPoint = found.transform;
                Debug.Log("Uff... Serwer znalaz³ bazê w ostatniej chwili.");
            }
            else
            {
                Debug.LogError("Nie znaleziono obiektu z tagiem 'DroneBase' na scenie!");
                return;
            }
        }

        if (_myDrone != null && _myDrone.Object != null && _myDrone.Object.IsValid)
        {
            Debug.Log("Dron ju¿ istnieje, nie spawnuje drugiego.");
            return;
        }

        Debug.Log($"Spawnujê drona w pozycji: {droneSpawnPoint.position}");

        NetworkObject droneObj = Runner.Spawn(dronePrefab, droneSpawnPoint.position, Quaternion.identity, info.Source);
        droneObj.AssignInputAuthority(info.Source);
        DroneController droneScript = droneObj.GetComponent<DroneController>();

        RPC_SetLocalDroneRef(droneScript);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SetLocalDroneRef(DroneController drone)
    {
        _myDrone = drone;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_OrderMove(Vector3 target)
    {
        if (_myDrone != null)
        {
            _myDrone.MoveToPosition(target);
        }
    }

    // --- RESZTA TWOJEGO KODU (BEZ ZMIAN) ---

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
                move = new Vector3(stick.x, 0f, stick.y);
            }

            Vector3 velocity = move * panSpeed;
            Vector3 stepTarget = camT.position + velocity * Time.deltaTime;
            stepTarget.y = camT.position.y;

            float alpha = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            camT.position = Vector3.Lerp(camT.position, stepTarget, alpha);

            if (useBounds)
            {
                camT.position = new Vector3(
                    Mathf.Clamp(camT.position.x, minXZ.x, maxXZ.x),
                    camT.position.y,
                    Mathf.Clamp(camT.position.z, minXZ.y, maxXZ.y)
                );
            }
        }

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
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((minXZ.x + maxXZ.x) * 0.5f, cameraHeight, (minXZ.y + maxXZ.y) * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(maxXZ.x - minXZ.x), 0.1f, Mathf.Abs(maxXZ.y - minXZ.y));
        Gizmos.DrawWireCube(center, size);
    }
#endif
}