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

    [Header("Drone visuals")]
    [SerializeField] private Material[] droneMaterials;
    [SerializeField] private int materialTargetSlot = 2;

    [Header("Mobile UI")]
    [SerializeField] private GameObject uiCanvasRoot;
    [SerializeField] private DroneSlotUI[] droneSlots; // trzy sloty ustaw w Inspectorze
    [SerializeField] private Button moveDroneButton;
    [SerializeField] private Button dropBombButton;

    // --- ZMIENNE WEWNÊTRZNE ---
    private InputSystem_Actions _controls;
    private bool _isLocal;
    private bool _cameraDetached;
    private bool _cameraIsRoot;
    private Transform _cameraTransform;
    private Vector2 _cameraInput = Vector2.zero;
    [Networked] public float CurrentHealth { get; set; }

    private DroneController[] _controlledDrones = new DroneController[3];
    private int _selectedSlot = 0;

    [SerializeField] private int _hp = 100;

    [SerializeField] private bool debugForceLocalInEditor = false;

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        CurrentHealth = _hp;
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

            // Podpinamy sloty UI (jeœli s¹)
            if (droneSlots != null && droneSlots.Length > 0)
            {
                for (int i = 0; i < droneSlots.Length; i++)
                {
                    var slot = droneSlots[i];
                    slot.slotIndex = i;
                    slot.OnSelected += OnSlotSelected;
                    slot.OnSpawnRequested += OnSlotSpawnRequested;
                }

                // ustaw domyœlne zaznaczenie
                _selectedSlot = Mathf.Clamp(_selectedSlot, 0, droneSlots.Length - 1);
                UpdateSlotVisuals();
            }
            else
            {
                Debug.LogWarning("OperatorController: droneSlots nie ustawione w Inspectorze.");
            }
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

        // Aktualizuj interakcje przycisków dla ka¿dego slotu
        for (int i = 0; i < _controlledDrones.Length; i++)
        {
            bool hasDrone = (_controlledDrones[i] != null && _controlledDrones[i].Object != null && _controlledDrones[i].Object.IsValid);
            if (droneSlots != null && i < droneSlots.Length)
            {
                droneSlots[i].SetSpawnInteractable(!hasDrone);
                // opcjonalnie zaktualizuj HP w UI jeœli masz dostêp do CurrentHP (tu prosty placeholder)
                if (hasDrone)
                {
                    // jeœli DroneController mia³by expose HP, mo¿esz tu ustawiæ pasek
                    // droneSlots[i].SetHPFill( ... );
                }
            }
        }

        bool selectedHasDrone = (_selectedSlot >= 0 && _selectedSlot < _controlledDrones.Length)
            && (_controlledDrones[_selectedSlot] != null && _controlledDrones[_selectedSlot].Object != null && _controlledDrones[_selectedSlot].Object.IsValid);

        if (moveDroneButton) moveDroneButton.interactable = selectedHasDrone;
        if (dropBombButton) dropBombButton.interactable = selectedHasDrone && _controlledDrones[_selectedSlot].IsBombReady;

        UpdateSlotVisuals();
    }

    private void UpdateSlotVisuals()
    {
        if (droneSlots == null) return;
        for (int i = 0; i < droneSlots.Length; i++)
        {
            droneSlots[i].SetSelected(i == _selectedSlot);
        }
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
    // Sloty wywo³uj¹ te metody przez eventy

    private void OnSlotSelected(int slot)
    {
        _selectedSlot = Mathf.Clamp(slot, 0, _controlledDrones.Length - 1);
        UpdateSlotVisuals();
    }

    private void OnSlotSpawnRequested(int slot)
    {
        RPC_RequestSpawnDrone(slot);
    }

    public void OnMoveButtonClicked()
    {
        if (!_isLocal) return;
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            RPC_OrderMove(_selectedSlot, hit.point);
        }
    }

    public void OnBombButtonClicked()
    {
        if (!_isLocal) return;
        RPC_OrderDropBomb(_selectedSlot);
    }

    // --- LOGIKA SIECIOWA (RPCS) ---

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnDrone(int slot, RpcInfo info = default)
    {
        // Sprawdzenie czy slot ju¿ ma drona
        if (slot < 0 || slot >= _controlledDrones.Length) return;

        if (_controlledDrones[slot] != null && _controlledDrones[slot].Object != null && _controlledDrones[slot].Object.IsValid)
            return;

        if (droneSpawnPoint == null)
        {
            Debug.LogError("OperatorController: Nie znaleziono DroneSpawnPoint (SprawdŸ Tag 'DroneBase')");
            return;
        }

        // Spawn drona (na serwerze)
        NetworkObject droneObj = Runner.Spawn(dronePrefab, droneSpawnPoint.position, Quaternion.identity, info.Source);
        DroneController droneScript = droneObj.GetComponent<DroneController>();

        // zapisujemy drona serwer-side w odpowiednim slocie
        _controlledDrones[slot] = droneScript;

        // wysy³amy informacjê do klienta (InputAuthority) aby zapisa³ referencjê lokalnie
        RPC_SetLocalDroneRef(droneScript, slot);

        int matIndex = Mathf.Clamp(slot, 0, (droneMaterials != null ? droneMaterials.Length - 1 : 0));
        RPC_SetDroneMaterial(droneScript, matIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_SetLocalDroneRef(DroneController drone, int slot)
    {
        if (slot < 0 || slot >= _controlledDrones.Length) return;
        _controlledDrones[slot] = drone;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_OrderMove(int slot, Vector3 target)
    {
        if (slot < 0 || slot >= _controlledDrones.Length) return;
        if (_controlledDrones[slot] != null)
        {
            _controlledDrones[slot].Server_MoveTo(target);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_OrderDropBomb(int slot)
    {
        if (slot < 0 || slot >= _controlledDrones.Length) return;
        if (_controlledDrones[slot] != null)
        {
            _controlledDrones[slot].Server_DropBomb();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetDroneMaterial(DroneController drone, int materialIndex)
    {
        if (drone == null) return;
        if (droneMaterials == null || droneMaterials.Length == 0) return;

        int matIdx = Mathf.Clamp(materialIndex, 0, droneMaterials.Length - 1);
        Material matToAssign = droneMaterials[matIdx];
        if (matToAssign == null) return;

        int targetSlot = Mathf.Max(0, materialTargetSlot);

        // U¿yjemy metody na DroneController, ¿eby nie odwo³ywaæ siê do prywatnych pól z zewn¹trz
        drone.ApplyMaterialToVisual(matToAssign, targetSlot);
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

        // odwi¹¿ eventy UI
        if (droneSlots != null)
        {
            for (int i = 0; i < droneSlots.Length; i++)
            {
                var slot = droneSlots[i];
                if (slot != null)
                {
                    slot.OnSelected -= OnSlotSelected;
                    slot.OnSpawnRequested -= OnSlotSpawnRequested;
                }
            }
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_ShowWinScreen()
    {
        if (GameEndScreenController.Instance != null)
            GameEndScreenController.Instance.ShowWin();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_ShowLoseScreen()
    {
        if (GameEndScreenController.Instance != null)
            GameEndScreenController.Instance.ShowLose();
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