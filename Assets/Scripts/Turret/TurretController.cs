using UnityEngine;
using Fusion;

public class TurretController : NetworkBehaviour, IDamageable
{
    [Header("Zdrowie")]
    [SerializeField] private int maxHP = 50;
    [SerializeField] private GameObject explosionPrefab;

    [Header("Ustawienia Celowania")]
    [SerializeField] private float range = 20f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private Transform partToRotate;
    [SerializeField] private Transform firePoint;

    [Header("Strzelanie")]
    [SerializeField] private NetworkPrefabRef bulletPrefab;
    [SerializeField] private float fireRate = 1f;

    [Networked] private TickTimer ShootTimer { get; set; }
    [Networked] private Quaternion TurretRotation { get; set; }
    [Networked] private int CurrentHP { get; set; }

    private Transform _target;

    public override void Spawned()
    {
        CurrentHP = maxHP;
        ShootTimer = TickTimer.CreateFromSeconds(Runner, 1f / fireRate);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            FindTarget();

            if (_target != null)
            {
                Vector3 direction = _target.position - partToRotate.position;
                direction.y = 0;

                if (direction != Vector3.zero)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    TurretRotation = Quaternion.Slerp(partToRotate.rotation, lookRotation, Runner.DeltaTime * rotationSpeed);

                    if (ShootTimer.ExpiredOrNotRunning(Runner))
                    {
                        Shoot();
                        ShootTimer = TickTimer.CreateFromSeconds(Runner, 1f / fireRate);
                    }
                }
            }
            else
            {
                //jak sie bedzie chcialo to tu mozna dodac jakis idle rotation
            }
        }
    }

    public override void Render()
    {
        if (partToRotate != null)
        {
            partToRotate.rotation = Quaternion.Slerp(partToRotate.rotation, TurretRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetLayer);
        float shortestDistance = Mathf.Infinity;
        Transform nearestTarget = null;

        foreach (var hit in hits)
        {
            if (hit.GetComponent<IDamageable>() != null || hit.GetComponentInParent<IDamageable>() != null)
            {
                float distanceToEnemy = Vector3.Distance(transform.position, hit.transform.position);
                if (distanceToEnemy < shortestDistance)
                {
                    shortestDistance = distanceToEnemy;
                    nearestTarget = hit.transform;
                }
            }
        }

        _target = nearestTarget;
    }

    private void Shoot()
    {
        if (bulletPrefab != null && firePoint != null)
        {
            Runner.Spawn(bulletPrefab, firePoint.position, firePoint.rotation);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }

    public void TakeDamage(int amount)
    {
        if (Object.HasStateAuthority)
        {
            CurrentHP -= amount;
            // Debug.Log($"Dzia³ko oberwa³o! HP: {CurrentHP}");

            if (CurrentHP <= 0)
            {
                Die();
            }
        }
    }

    private void Die()
    {
        if (explosionPrefab != null)
        {
            if (explosionPrefab.GetComponent<NetworkObject>() != null)
                Runner.Spawn(explosionPrefab, transform.position, Quaternion.identity);
        }

        Runner.Despawn(Object);
    }
}
