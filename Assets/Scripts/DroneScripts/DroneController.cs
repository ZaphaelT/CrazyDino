using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class DroneController : NetworkBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private int maxHP = 30;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float flightHeight = 3.0f;

    [Header("Bomb")]
    [SerializeField] private NetworkPrefabRef bombPrefab;
    [SerializeField] private Transform bombSpawnPoint;
    [SerializeField] private float cooldownTime = 5f;

    [Header("Visuals")]
    [SerializeField] private Transform visualModel;

    [Networked] private int CurrentHP { get; set; }
    [Networked] private TickTimer BombCooldown { get; set; }

    private NavMeshAgent _agent;

    public override void Spawned()
    {
        CurrentHP = maxHP;
        _agent = GetComponent<NavMeshAgent>();

        // Model wizualny w górê
        if (visualModel != null)
            visualModel.localPosition = new Vector3(0, flightHeight, 0);

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.updateRotation = true;
            _agent.updateUpAxis = true;

            // TYLKO SERWER MO¯E U¯YWAÆ NAVMESHA
            // Dziêki temu Klient nie walczy o pozycjê
            if (Object.HasStateAuthority)
            {
                _agent.enabled = true;
                // Fix na startow¹ pozycjê (zapobiega skokowi na 0,0,0)
                _agent.Warp(transform.position);
            }
            else
            {
                _agent.enabled = false;
            }
        }
    }

    // Metoda wywo³ywana TYLKO przez Serwer (z OperatorController)
    public void Server_MoveTo(Vector3 target)
    {
        if (_agent != null && _agent.enabled)
        {
            _agent.SetDestination(target);
        }
    }

    // Metoda wywo³ywana TYLKO przez Serwer (z OperatorController)
    public void Server_DropBomb()
    {
        if (BombCooldown.ExpiredOrNotRunning(Runner))
        {
            BombCooldown = TickTimer.CreateFromSeconds(Runner, cooldownTime);
            if (bombSpawnPoint != null)
            {
                Runner.Spawn(bombPrefab, bombSpawnPoint.position, Quaternion.identity);
            }
        }
    }

    // Helper dla UI
    public bool IsBombReady => BombCooldown.ExpiredOrNotRunning(Runner);

    public void TakeDamage(int amount)
    {
        if (Object.HasStateAuthority)
        {
            CurrentHP -= amount;
            if (CurrentHP <= 0)
            {
                Runner.Despawn(Object);
            }
        }
    }

    public void ApplyMaterialToVisual(Material materialToAssign, int targetSlot)
    {
        if (materialToAssign == null)
            return;

        Transform root = visualModel != null ? visualModel : transform;
        var renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
                continue;

            if (targetSlot >= 0 && targetSlot < mats.Length)
            {
                Material[] newMats = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++) newMats[i] = mats[i];
                newMats[targetSlot] = materialToAssign;

                r.materials = newMats;
            }
        }
    }
}