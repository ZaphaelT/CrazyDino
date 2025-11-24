using UnityEngine;
using Fusion;

public class BombProjectile : NetworkBehaviour
{
    [SerializeField] private float fallSpeed = 20f;
    [SerializeField] private int damage = 15;
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private LayerMask hitLayers; // Zaznacz: Default (ziemia) i warstwê Dina
    [SerializeField] private GameObject explosionEffect; // Opcjonalny prefab wybuchu

    public override void FixedUpdateNetwork()
    {
        // Prosta symulacja spadania w dó³
        transform.position += Vector3.down * fallSpeed * Runner.DeltaTime;
    }

    // Wykrycie uderzenia (musi byæ IsTrigger w Colliderze bomby)
    private void OnTriggerEnter(Collider other)
    {
        // Kolizje liczy tylko serwer
        if (!Object.HasStateAuthority) return;

        // Sprawdzamy czy trafiliœmy w coœ z dozwolonej warstwy (ziemia lub dino)
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        // 1. Zadaj obra¿enia obszarowe
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, hitLayers);
        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        // 2. Efekt wizualny (spawn sieciowy)
        if (explosionEffect != null)
        {
            Runner.Spawn(explosionEffect, transform.position, Quaternion.identity);
        }

        // 3. Zniszcz bombê
        Runner.Despawn(Object);
    }
}
