using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private NetworkCharacterController _cc;

    [SerializeField] private Camera playerCamera;
    private float _speed;

    private bool _isLocal;
    private bool _cameraDetached;
    private Vector3 _cameraOffset;
    private Quaternion _cameraRotationOnDetach;

    // --- fields for simple client-side prediction / smoothing
    private Vector3 _predictedPosition;
    private Vector3 _lastAuthoritativePosition;
    private float _reconciliationLerp = 0.05f; // jak szybko klient dogania serwer (mniejsze = szybsze doganianie)
    private bool _hasPredictedPosition;

    public override void Spawned()
    {
        _cc = GetComponent<NetworkCharacterController>();

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

        // inicjalizacja predykcji
        _predictedPosition = transform.position;
        _lastAuthoritativePosition = transform.position;
        _hasPredictedPosition = true;
    }

    void LateUpdate()
    {
        if (_isLocal && playerCamera != null)
        {
            playerCamera.transform.position = transform.position + _cameraOffset;
            playerCamera.transform.rotation = _cameraRotationOnDetach;
        }

        // Je¿eli mamy lokaln¹ predykcjê (InputAuthority ale nie StateAuthority),
        // utrzymuj transform wizualny zgodnie z predykcj¹ / rekonsyliacj¹
        if (Object.HasInputAuthority && !Object.HasStateAuthority)
        {
            // Lerp od aktualnej pozycji (otrzymanej z network) do pozycji predykowanej
            if (_hasPredictedPosition)
            {
                transform.position = Vector3.Lerp(transform.position, _predictedPosition, Mathf.Clamp01(_reconciliationLerp));
            }
        }
    }

    void OnDestroy()
    {
        if (_cameraDetached && playerCamera != null)
            Destroy(playerCamera.gameObject);
    }

    public override void FixedUpdateNetwork()
    {
        // Jeœli mamy StateAuthority (serwer / host), to wykonujemy authoritative movement
        if (Object.HasStateAuthority)
        {
            Vector3 desiredVelocity = Vector3.zero;

            if (GetInput(out NetworkInputData data))
            {
                var inputDir = data.direction;
                if (inputDir.sqrMagnitude > 0f)
                {
                    var dirNormalized = inputDir.normalized;
                    desiredVelocity = dirNormalized * _speed;
                }
            }

            if (_cc != null)
                _cc.Move(desiredVelocity);
            else
                transform.position += desiredVelocity * Runner.DeltaTime;

            // aktualizujemy pozycjê autorytatywn¹ (do synchronizacji z klientami)
            _lastAuthoritativePosition = transform.position;
        }
        else
        {
            // Je¿eli nie mamy StateAuthority, ale mamy InputAuthority - przewidujemy lokalnie.
            if (Object.HasInputAuthority)
            {
                if (GetInput(out NetworkInputData data))
                {
                    var inputDir = data.direction;
                    Vector3 predictedVelocity = Vector3.zero;
                    if (inputDir.sqrMagnitude > 0f)
                    {
                        predictedVelocity = inputDir.normalized * _speed;
                    }

                    // prosty integraor pozycji w tickach lokalnych (u¿ywa Runner.DeltaTime do spójnoœci z symulacj¹)
                    _predictedPosition += predictedVelocity * Runner.DeltaTime;
                    _hasPredictedPosition = true;
                }
            }
        }
    }

    // Opcjonalnie: gdy stan autorytatywny zostanie zaktualizowany, zrób lekk¹ rekonsyliacjê:
    public override void Render()
    {
        // Render wywo³ywany ka¿d¹ klatkê - mo¿emy porównaæ i delikatnie skorygowaæ predykcjê
        if (Object.HasInputAuthority && !Object.HasStateAuthority)
        {
            // Jeœli serwer przes³a³ aktualn¹ pozycjê (transform ju¿ zaktualizowany do stanu serwera),
            // to schowkujemy j¹ do reconcilation i delikatnie dopasowujemy predictedPosition.
            Vector3 authoritative = transform.position;

            // jeœli du¿a rozbie¿noœæ - ustaw predicted na authoritative, inaczej delikatnie dopasuj
            float dist = Vector3.Distance(authoritative, _predictedPosition);
            if (dist > 1.0f)
            {
                // zbyt du¿a ró¿nica -> natychmiast synchronizuj (zapobiega du¿ym „teleportom”)
                _predictedPosition = authoritative;
            }
            else
            {
                // lekkie dopasowanie predicted -> mniejsze „skoki”
                _predictedPosition = Vector3.Lerp(_predictedPosition, authoritative, 0.1f);
            }
        }
    }
}