using Fusion;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private NetworkCharacterController _cc;

    [SerializeField] private Camera playerCamera; // opcjonalnie przypisz w prefabie lub znajdzie child
    private float _speed; // bêdzie zsynchronizowane z _cc.maxSpeed jeœli dostêpne

    // kamera follow (nie obracamy jej razem z graczem)
    private bool _isLocal;
    private bool _cameraDetached;
    private Vector3 _cameraOffset;
    private Quaternion _cameraRotationOnDetach;

    // Upewniamy siê, ¿e komponent jest pobrany przy spawn
    public override void Spawned()
    {
        _cc = GetComponent<NetworkCharacterController>();

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
        // Ruch aplikuje tylko instancja z state authority
        if (!Object.HasStateAuthority)
            return;

        // Domyœlnie brak prêdkoœci
        Vector3 desiredVelocity = Vector3.zero;

        if (GetInput(out NetworkInputData data))
        {
            // DEBUG: zobacz, jakie dane inputu trafi³y na obiekt
            Debug.Log($"[STATE] FixedUpdateNetwork: ObjId={Object.Id} InputAuthority={Object.InputAuthority} rawDir={data.direction}");

            // Je¿eli mamy input, skaluje go do prêdkoœci maksymalnej kontrolera.
            // Najpierw ograniczamy magnitudê (keyboard mo¿e dawaæ sqrt(2) przy diagonalu)
            var inputDir = data.direction;
            if (inputDir.sqrMagnitude > 0f)
            {
                var dirNormalized = inputDir.normalized;
                desiredVelocity = dirNormalized * _speed; // units per second
            }
            // else desiredVelocity zostaje zero
        }

        // WA¯NE: przeka¿emy prêdkoœæ (units/sec) do kontrolera — on wewnêtrznie poradzi siê z delta/time/acceleration/braking
        if (_cc != null)
        {
            _cc.Move(desiredVelocity);
        }
        else
        {
            // fallback: jeœli kontroler nie istnieje, stosuj zwyk³y przesuw (tu z deltaTime)
            transform.position += desiredVelocity * Runner.DeltaTime;
        }
    }
}