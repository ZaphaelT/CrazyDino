using Fusion;
using UnityEngine;
using UnityEngine.AI;
using static Unity.Collections.Unicode;

public class DroneController : NetworkBehaviour, IDamageable
{
    [Header("Statystyki")]
    [SerializeField] private int maxHP = 30;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float flightHeight = 3.0f; // Wysokoœæ "wizualna"

    [Header("Bomba")]
    [SerializeField] private NetworkPrefabRef bombPrefab;
    [SerializeField] private Transform bombSpawnPoint; // Punkt pod dronem
    [SerializeField] private float cooldownTime = 5f;

    [Header("Setup")]
    [SerializeField] private Transform visualModel; // Ten obiekt przesuniemy w górê

    [Networked] private int CurrentHP { get; set; }
    [Networked] private TickTimer BombCooldown { get; set; }

    private NavMeshAgent _agent;

    public override void Spawned()
    {
        CurrentHP = maxHP;
        _agent = GetComponent<NavMeshAgent>();

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.updateRotation = true;
            _agent.updateUpAxis = true;
        }

        // Ustawienie modelu wizualnego w powietrzu
        if (visualModel != null)
        {
            visualModel.localPosition = new Vector3(0, flightHeight, 0);
        }
    }

    // Funkcja ruchu - wywo³ywana przez serwer
    public void MoveToPosition(Vector3 targetPos)
    {
        if (Object.HasStateAuthority && _agent != null)
        {
            _agent.SetDestination(targetPos);
        }
    }

    // Funkcja zrzutu bomby
    public void TryDropBomb()
    {
        // Tylko w³aœciciel drona (Operator) mo¿e to wywo³aæ
        if (Object.HasInputAuthority)
        {
            RPC_DropBomb();
        }
    }

    // Helper dla UI - czy bomba gotowa?
    public bool IsBombReady => BombCooldown.ExpiredOrNotRunning(Runner);

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_DropBomb()
    {
        if (BombCooldown.ExpiredOrNotRunning(Runner))
        {
            // Reset licznika
            BombCooldown = TickTimer.CreateFromSeconds(Runner, cooldownTime);

            // Spawn bomby
            if (bombSpawnPoint != null)
            {
                Runner.Spawn(bombPrefab, bombSpawnPoint.position, Quaternion.identity);
            }
        }
    }

    // Obs³uga obra¿eñ od Dinozaura
    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHP -= amount;
        if (CurrentHP <= 0)
        {
            Runner.Despawn(Object);
        }
    }
}
