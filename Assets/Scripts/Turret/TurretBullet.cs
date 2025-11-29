using UnityEngine;
using Fusion;

public class TurretBullet : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifeTime = 3f; // Zniknie po 3 sek, ¿eby nie zaœmiecaæ mapy
    [SerializeField] private LayerMask hitLayers; // Warstwy: Dino, Ziemia, Œciany

    [Networked] private TickTimer LifeTimer { get; set; }

    public override void Spawned()
    {
        // Ustawiamy czas ¿ycia
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        // 1. Obs³uga czasu ¿ycia
        if (LifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        // 2. Ruch do przodu (lokalnie w osi Z pocisku)
        transform.position += transform.forward * speed * Runner.DeltaTime;
    }

    // Wykrywanie kolizji
    private void OnTriggerEnter(Collider other)
    {
        // Tylko serwer liczy trafienia
        if (!Object.HasStateAuthority) return;

        // Sprawdzamy czy trafiliœmy w dozwolon¹ warstwê
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            // Próba zadania obra¿eñ (np. Dinozaurowi)
            var damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            // Zniszcz pocisk po trafieniu
            Runner.Despawn(Object);
        }
    }
}
