using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BombProjectile : NetworkBehaviour
{
    [Header("Ustawienia")]
    [SerializeField] private int damage = 15;
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private GameObject explosionEffect;

    private NetworkId _ownerId;

    public override void Spawned()
    {
    }

    public void SetOwner(NetworkId id)
    {
        _ownerId = id;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Tylko serwer liczy fizykê
        if (!Object.HasStateAuthority) return;


        if (other.GetComponent<DroneController>() != null || other.GetComponentInParent<DroneController>() != null)
        {
            return;
        }

        // 3. Sprawdzamy warstwy (czy to Ziemia lub Dino)
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        // Szukamy wszystkich ofiar w promieniu wybuchu
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, hitLayers);

        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            var droneCheck = hit.GetComponent<DroneController>() ?? hit.GetComponentInParent<DroneController>();

            // Zadajemy obra¿enia TYLKO jeœli to nie jest Dron
            // (Chyba ¿e chcesz, ¿eby dron móg³ oberwaæ od fali uderzeniowej jak leci nisko - wtedy usuñ drug¹ czêœæ warunku)
            if (damageable != null && droneCheck == null)
            {
                damageable.TakeDamage(damage);
            }
        }

        // --- SPAWN EFEKTU I DESPAWN BOMBY ---

        // Zabezpieczenie przed czerwonym b³êdem "No NetworkObject"
        if (explosionEffect != null)
        {
            // Jeœli prefab ma NetworkObject -> Spawnujemy przez sieæ
            if (explosionEffect.GetComponent<NetworkObject>() != null)
            {
                Runner.Spawn(explosionEffect, transform.position, Quaternion.identity);
            }
            // Jeœli to zwyk³y Particle System bez NetworkObject -> Spawnujemy lokalnie (mniej bezpieczne, ale dzia³a)
            else
            {
                Instantiate(explosionEffect, transform.position, Quaternion.identity);
            }
        }

        // Niszczymy bombê
        Runner.Despawn(Object);
    }
}