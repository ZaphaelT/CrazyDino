using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BombProjectile : NetworkBehaviour
{
    [SerializeField] private int damage = 15;
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private GameObject explosionEffect;

    public override void Spawned()
    {
    }

    private void OnTriggerEnter(Collider other)
    {
        // Tylko serwer liczy wybuchy
        if (!Object.HasStateAuthority) return;

        // Debug: Poka¿ w co uderzyliœmy (jeœli nie dzia³a, odkomentuj liniê ni¿ej)
        Debug.Log($"Bomba dotknê³a: {other.gameObject.name} na warstwie {other.gameObject.layer}");

        if (other.GetComponent<DroneController>() != null)
        {
            return;
        }
        // Sprawdzamy czy warstwa obiektu pasuje do naszej maski
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            Explode();
        }
    }

    private void Explode()
    {
        // Zadaj obra¿enia
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, hitLayers);
        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        // Efekt wizualny
        if (explosionEffect != null)
        {
            Runner.Spawn(explosionEffect, transform.position, Quaternion.identity);
        }

        // Zniszcz bombê
        Runner.Despawn(Object);
    }
}