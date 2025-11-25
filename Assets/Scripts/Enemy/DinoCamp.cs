using Fusion;
using UnityEngine;

public class DinoCamp : NetworkBehaviour
{
    [Header("Przypisz Dinozaura ze sceny")]
    [SerializeField] private EnemyDino myDino;

    [Header("Ustawienia")]
    [SerializeField] private float respawnTime = 60f;

    // Pozycja startowa (zapamiêtana przy starcie gry)
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    [Networked] private TickTimer RespawnTimer { get; set; }
    private bool _isTimerRunning = false;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            if (myDino != null)
            {
                // Zapamiêtaj gdzie dinozaur sta³ na pocz¹tku
                _startPosition = myDino.transform.position;
                _startRotation = myDino.transform.rotation;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || myDino == null) return;

        // Jeœli dinozaur nie ¿yje i nie odliczamy jeszcze czasu...
        if (myDino.IsDead && !_isTimerRunning)
        {
            Debug.Log($"[Camp] Dino pad³. Respawn za {respawnTime}s");
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnTime);
            _isTimerRunning = true;
        }

        // Jeœli odliczamy czas...
        if (_isTimerRunning)
        {
            if (RespawnTimer.Expired(Runner))
            {
                // Czas min¹³ -> O¿ywiamy!
                myDino.Respawn(_startPosition, _startRotation);
                _isTimerRunning = false;
            }
        }
    }

    // Wizualizacja w edytorze
    private void OnDrawGizmos()
    {
        if (myDino != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, myDino.transform.position);
        }
    }
}