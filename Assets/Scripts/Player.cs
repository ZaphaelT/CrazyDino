using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private NetworkCharacterController _cc;
    private CharacterController _localController;

    [SerializeField] private Camera playerCamera; // opcjonalnie przypisz w prefabie lub znajdzie child
    private float _speed; // bêdzie zsynchronizowane z _cc.maxSpeed jeœli dostêpne

    // kamera follow (nie obracamy jej razem z graczem)
    private bool _isLocal;
    private bool _cameraDetached;
    private Vector3 _cameraOffset;
    private Quaternion _cameraRotationOnDetach;

    // predykcja klienta (lokalna wizualizacja)
    private Vector3 _predictedHorizontalVelocity = Vector3.zero;

    // Upewniamy siê, ¿e komponent jest pobrany przy spawn
    public override void Spawned()
    {
        _cc = GetComponent<NetworkCharacterController>();
        TryGetComponent(out _localController);

        // jeœli kontroler istnieje, u¿ywaj jego maxSpeed jako Ÿród³a prawdziwej prêdkoœci
        if (_cc != null)
            _speed = _cc.maxSpeed;
        else
            _speed = 5f; // fallback

        // ZnajdŸ kamerê jeœli nie przypisano przez Inspector
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        _isLocal = Object.HasInputAuthority;

        // Je¿eli mamy kamerê w prefabu - aktywuj j¹ tylko dla lokalnego gracza
        if (playerCamera != null)
        {
            // local: od³¹czamy kamerê od gracza tak, aby nie dziedziczy³a rotacji transformu
            if (_isLocal)
            {
                // zapamiêtaj offset/rotacjê i odczep kamerê
                _cameraOffset = playerCamera.transform.position - transform.position;
                _cameraRotationOnDetach = playerCamera.transform.rotation;

                // odczepienie zapobiega dziedziczeniu rotacji gracza
                playerCamera.transform.SetParent(null);
                _cameraDetached = true;

                playerCamera.gameObject.SetActive(true);
                var audio = playerCamera.GetComponent<AudioListener>();
                if (audio != null)
                    audio.enabled = true;
            }
            else
            {
                // nie-lokalne: wy³¹czamy kamerê prefabow¹
                playerCamera.gameObject.SetActive(false);
            }
        }

        Debug.Log(
            $"Player Spawned: ObjId={Object.Id} Name={gameObject.name} HasStateAuthority={Object.HasStateAuthority} " +
            $"HasInputAuthority={Object.HasInputAuthority} InputAuthority={Object.InputAuthority} RunnerLocalPlayer={Runner.LocalPlayer} speed={_speed}"
        );
    }

    void LateUpdate()
    {
        // Tylko lokalna kamera ma follow; robimy to w LateUpdate ¿eby nad¹¿yæ za ruchem
        if (_isLocal && playerCamera != null)
        {
            // Ustaw pozycjê kamery wzglêdem gracza (offset zapisany przy detach)
            playerCamera.transform.position = transform.position + _cameraOffset;

            // Zachowaj sta³¹ rotacjê (nie dziedzicz rotacji gracza)
            playerCamera.transform.rotation = _cameraRotationOnDetach;
        }
    }

    void OnDestroy()
    {
        // jeœli odczepiliœmy kamerê od prefabu, usuñ j¹ przy zniszczeniu gracza
        if (_cameraDetached && playerCamera != null)
        {
            Destroy(playerCamera.gameObject);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // NIE zwracamy wczeœniej — potrzebujemy obs³ugi zarówno dla state authority (autoratywne) jak i input authority (predykcja)
        // Domyœlnie brak prêdkoœci
        Vector3 inputDirection = Vector3.zero;

        if (GetInput(out NetworkInputData data))
        {
            // DEBUG: zobacz, jakie dane inputu trafi³y na obiekt
            //Debug.Log($"[STATE] FixedUpdateNetwork: ObjId={Object.Id} InputAuthority={Object.InputAuthority} rawDir={data.direction}");
            inputDirection = data.direction;
            if (inputDirection.sqrMagnitude > 0f)
                inputDirection = inputDirection.normalized;
        }

        // 1) State Authority robi faktyczny, autoratywny ruch
        if (Object.HasStateAuthority)
        {
            // przekazujemy kierunek (nie skalujemy) — NetworkCharacterController normalizuje i u¿ywa acceleration/braking/maxSpeed
            if (inputDirection.sqrMagnitude > 0f)
                _cc?.Move(inputDirection);
            else
                _cc?.Move(Vector3.zero);
            // (dane sieciowe i Velocity s¹ aktualizowane wewn¹trz Move)
            return;
        }

        // 2) Jeœli nie mamy state authority ale mamy InputAuthority -> symulujemy lokalnie (predykcja wizualna)
        if (Object.HasInputAuthority)
        {
            // jeœli mamy NetworkCharacterController, korzystaj z jego parametrów do dopasowania predykcji
            float accel = _cc != null ? _cc.acceleration : 10f;
            float braking = _cc != null ? _cc.braking : 10f;
            float maxSpeed = _cc != null ? _cc.maxSpeed : _speed;

            // Predykcja prostego modelu: interpoluj prêdkoœæ poziom¹ w kierunku targetu
            Vector3 targetHorizontal = inputDirection.sqrMagnitude > 0f ? inputDirection * maxSpeed : Vector3.zero;

            // u¿ywamy Runner.DeltaTime (tick delta) do predykcji spójnej z serwerem
            float dt = Runner.DeltaTime;

            // przyspieszenie / hamowanie: lerp w stronê targetu, tempo zale¿ne od accel/braking
            if (targetHorizontal.sqrMagnitude > 0f)
            {
                _predictedHorizontalVelocity = Vector3.MoveTowards(_predictedHorizontalVelocity, targetHorizontal, accel * dt);
            }
            else
            {
                _predictedHorizontalVelocity = Vector3.MoveTowards(_predictedHorizontalVelocity, Vector3.zero, braking * dt);
            }

            // zastosuj lokalny ruch przez CharacterController jeœli dostêpny (bardziej fizyczne ni¿ transform.translate)
            if (_localController != null)
            {
                // zachowaj Y z aktualnego Velocity (prosta grawitacja/pozycja) — tu utrzymujemy tylko poziomy ruch
                Vector3 move = new Vector3(_predictedHorizontalVelocity.x, 0f, _predictedHorizontalVelocity.z) * dt;
                _localController.Move(move);
            }
            else
            {
                // fallback: porusz transformem
                transform.position += new Vector3(_predictedHorizontalVelocity.x, 0f, _predictedHorizontalVelocity.z) * dt;
            }

            // UWAGA: autoratywny serwer w kolejnych tickach mo¿e skorygowaæ pozycjê — to normalne
            return;
        }

        // 3) Brak prawa do inputu ani stanu — nic nie robimy
    }
}