using UnityEngine;
using Fusion;

public class TurretController : NetworkBehaviour
{
    [Header("Ustawienia Celowania")]
    [SerializeField] private float range = 20f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private LayerMask targetLayer; // Warstwa Dinozaura (np. Damageable)
    [SerializeField] private Transform partToRotate; // Obiekt, który ma siê krêciæ (np. Cylinder.005)
    [SerializeField] private Transform firePoint;    // Pusty obiekt na koñcu lufy

    [Header("Strzelanie")]
    [SerializeField] private NetworkPrefabRef bulletPrefab;
    [SerializeField] private float fireRate = 1f; // Strza³y na sekundê

    [Networked] private TickTimer ShootTimer { get; set; }
    [Networked] private Quaternion TurretRotation { get; set; } // Synchronizujemy obrót przez sieæ

    private Transform _target;

    public override void Spawned()
    {
        // Ustawiamy pocz¹tkowy timer
        ShootTimer = TickTimer.CreateFromSeconds(Runner, 1f / fireRate);
    }

    public override void FixedUpdateNetwork()
    {
        // Logikê liczy tylko serwer
        if (Object.HasStateAuthority)
        {
            FindTarget();

            if (_target != null)
            {
                // Obliczanie rotacji
                Vector3 direction = _target.position - partToRotate.position;
                direction.y = 0; // Ignorujemy ró¿nicê wysokoœci, ¿eby dzia³ko nie przechyla³o siê dziwnie

                if (direction != Vector3.zero)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(direction);
                    // P³ynny obrót
                    TurretRotation = Quaternion.Slerp(partToRotate.rotation, lookRotation, Runner.DeltaTime * rotationSpeed);

                    // Strzelanie
                    if (ShootTimer.ExpiredOrNotRunning(Runner))
                    {
                        Shoot();
                        ShootTimer = TickTimer.CreateFromSeconds(Runner, 1f / fireRate);
                    }
                }
            }
            else
            {
                // Brak celu? Mo¿na tu dodaæ np. obracanie siê dooko³a (idle)
            }
        }
    }

    // Funkcja Render s³u¿y do p³ynnego wyœwietlania zmian u klientów
    public override void Render()
    {
        if (partToRotate != null)
        {
            // Klient ustawia rotacjê na podstawie tego, co przys³a³ serwer (zmienna sieciowa TurretRotation)
            partToRotate.rotation = Quaternion.Slerp(partToRotate.rotation, TurretRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void FindTarget()
    {
        // Szukamy obiektów w zasiêgu
        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetLayer);
        float shortestDistance = Mathf.Infinity;
        Transform nearestTarget = null;

        foreach (var hit in hits)
        {
            // Sprawdzamy czy to Dino (ma IDamageable)
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
}
