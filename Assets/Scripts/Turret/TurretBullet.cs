using UnityEngine;
using Fusion;

public class TurretBullet : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private LayerMask hitLayers;

    [Networked] private TickTimer LifeTimer { get; set; }

    public override void Spawned()
    {
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeTime);
    }

    public override void FixedUpdateNetwork()
    {
        if (LifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
            return;
        }

        transform.position += transform.forward * speed * Runner.DeltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            var damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }

            Runner.Despawn(Object);
        }
    }
}
