using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class DroneController : NetworkBehaviour, IDamageable
{
    [Header("Statystyki")]
    [SerializeField] private int maxHP = 30;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float flightHeight = 10.0f;

    [Header("Bomba")]
    [SerializeField] private NetworkPrefabRef bombPrefab;
    [SerializeField] private Transform bombSpawnPoint;
    [SerializeField] private float cooldownTime = 5f;

    [Header("Setup")]
    [SerializeField] private Transform visualModel;

    [Networked] private int CurrentHP { get; set; }
    [Networked] private TickTimer BombCooldown { get; set; }

    private NavMeshAgent _agent;

    public override void Spawned()
    {
        CurrentHP = maxHP;
        _agent = GetComponent<NavMeshAgent>();

        // Ustawienie modelu wizualnego
        if (visualModel != null)
            visualModel.localPosition = new Vector3(0, flightHeight, 0);

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.updateRotation = true;
            _agent.updateUpAxis = true;

            // --- KLUCZOWA POPRAWKA RUCHU ---
            if (Object.HasStateAuthority)
            {
                // Jesteœmy SERWEREM (Dino-Host lub Operator-Host):
                // My zarz¹dzamy ruchem. W³¹czamy Agenta.
                _agent.enabled = true;

                // Fix na startow¹ pozycjê (Warp)
                _agent.Warp(transform.position);
            }
            else
            {
                // Jesteœmy KLIENTEM (Operator-Klient):
                // Nie mamy prawa decydowaæ o ruchu. Wy³¹czamy Agenta ca³kowicie.
                // Bêdziemy tylko odbieraæ pozycjê przez NetworkTransform.
                _agent.enabled = false;
            }
        }
    }

    // Funkcja ruchu
    public void MoveToPosition(Vector3 targetPos)
    {
        // Tylko Serwer ma w³¹czonego agenta, wiêc tylko on wykona ten kod
        if (Object.HasStateAuthority && _agent != null && _agent.isOnNavMesh)
        {
            _agent.SetDestination(targetPos);
        }
    }

    // --- POPRAWKA BOMBY ---
    public void TryDropBomb()
    {
        // Przypadek 1: Jestem Hostem (mam w³adzê absolutn¹)
        if (Object.HasStateAuthority)
        {
            DropBombInternal(); // Robiê to natychmiast, bez RPC
        }
        // Przypadek 2: Jestem Klientem (mam w³adzê nad wejœciem)
        else if (Object.HasInputAuthority)
        {
            RPC_DropBomb(); // Wysy³am proœbê do serwera
        }
    }

    // Wewnêtrzna logika zrzutu (wspólna dla RPC i Hosta)
    private void DropBombInternal()
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_DropBomb()
    {
        // Serwer odbiera proœbê i wykonuje zrzut
        DropBombInternal();
    }

    public bool IsBombReady => BombCooldown.ExpiredOrNotRunning(Runner);

    public void TakeDamage(int amount)
    {
        if (!Object.HasStateAuthority) return;
        CurrentHP -= amount;
        if (CurrentHP <= 0) Runner.Despawn(Object);
    }
}