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

        // 1. Ustawienie modelu wizualnego (lewitacja)
        if (visualModel != null)
        {
            visualModel.localPosition = new Vector3(0, flightHeight, 0);
        }

        if (_agent != null)
        {
            _agent.speed = moveSpeed;
            _agent.updateRotation = true;
            _agent.updateUpAxis = true;

            // --- FIX NA TELEPORTACJÊ 0,0,0 ---

            // Krok A: Jeœli jesteœmy serwerem (State Authority), to my decydujemy o pozycji
            if (Object.HasStateAuthority)
            {
                // Wy³¹czamy agenta, ¿eby nie "walczy³"
                _agent.enabled = false;

                // Upewniamy siê, ¿e pozycja fizyczna jest taka jak spawnu
                // (Fusion ju¿ to ustawi³ przy spawnie, ale dla pewnoœci)
                transform.position = transform.position;

                // W³¹czamy agenta
                _agent.enabled = true;

                // KLUCZOWE: Warp() informuje wewnêtrzny system nawigacji "Jesteœ TUTAJ, zaakceptuj to"
                bool warped = _agent.Warp(transform.position);

                if (warped)
                {
                    Debug.Log($"Dron zespawnowany poprawnie na: {transform.position}");
                }
                else
                {
                    Debug.LogError($"Dron nie móg³ znaleŸæ NavMesha w pozycji {transform.position}! Czy spawn point na pewno dotyka niebieskiej siatki?");
                }
            }
            else
            {
                // Jeœli jesteœmy Klientem (nie serwerem), wy³¹czamy NavMeshAgenta ca³kowicie.
                // Klient ma tylko widzieæ drona tam, gdzie ka¿e NetworkTransform.
                // Jeœli tego nie zrobisz, Agent u Klienta te¿ bêdzie próbowa³ ustawiaæ pozycjê i bêdzie szarpaæ.
                _agent.enabled = false;
            }
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
