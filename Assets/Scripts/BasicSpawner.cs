using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;


public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private InputSystem_Actions _controls;

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("OnConnectedToServer");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"OnConnectFailed: reason={reason} remote={remoteAddress}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        Debug.Log("OnConnectRequest");
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        Debug.Log("OnCustomAuthenticationResponse");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"OnDisconnectedFromServer: reason={reason}");
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("OnHostMigration");
    }

    private void OnEnable()
    {
        if (_controls == null)
            _controls = new InputSystem_Actions();

        // W³¹czamy mapê Player (aktywnuje wszystkie akcje w tej mapie)
        _controls.Player.Enable();
    }

    private void OnDisable()
    {
        if (_controls != null)
            _controls.Player.Disable();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        Vector2 move = Vector2.zero;

        if (_controls != null)
        {
            try
            {
                move = _controls.Player.Move.ReadValue<Vector2>();
            }
            catch (Exception)
            {
                move = Vector2.zero;
            }
        }

        // zachowujemy kierunek ruchu (dla dino)
        data.direction = new Vector3(move.x, 0f, move.y);

        // zapisujemy ten sam wektor jako input kamery (operator u¿yje tego pola)
        data.camera = move;

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.Log($"OnInputMissing: player={player}");
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"OnObjectEnterAOI: obj={obj.Id} for player={player}");
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        Debug.Log($"OnObjectExitAOI: obj={obj.Id} for player={player}");
    }

    [SerializeField] private NetworkPrefabRef _dinosaurPrefab;
    [SerializeField] private NetworkPrefabRef _operatorPrefab;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    [SerializeField] private Transform dinoSpawnPoint;
    [SerializeField] private Transform operatorSpawnPoint;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"OnPlayerJoined called on runner.IsServer={runner.IsServer} RunnerLocalPlayer={runner.LocalPlayer} player={player}");

        if (runner.IsServer)
        {
            int playerIndex = _spawnedCharacters.Count;

            // wybór prefabów (dostosuj jeœli chcesz innej logiki)
            NetworkPrefabRef prefab = (playerIndex == 0) ? _operatorPrefab : _dinosaurPrefab; // tu zmieniamy kto jest pierwszy 0=operator,1=dino

            Vector3 spawnPosition;
            int spawnIndex = prefab.Equals(_operatorPrefab) ? 1 : 0;

            // wybierz przypisany punkt zgodnie z typem (0=dino,1=operator)
            Transform selectedPoint = (spawnIndex == 1) ? operatorSpawnPoint : dinoSpawnPoint;

            if (selectedPoint == null)
            {
                Debug.LogWarning($"Spawn point at index {spawnIndex} is null. Falling back to default position.");
                spawnPosition = new Vector3(playerIndex * 3, 1, 0);
            }
            else
            {
                spawnPosition = selectedPoint.position;
                Debug.Log($"Spawning player {player} at spawnIndex={spawnIndex} position={spawnPosition}");
            }

            Quaternion spawnRotation = (selectedPoint != null) ? selectedPoint.rotation : Quaternion.identity;
            NetworkObject networkPlayerObject = runner.Spawn(prefab, spawnPosition, spawnRotation, player);

            if (networkPlayerObject != null)
            {
                networkPlayerObject.name = $"Player_Obj_Player{player.RawEncoded}";
                _spawnedCharacters.Add(player, networkPlayerObject);
                // POPRAWKA: u¿ywamy prefab, nie playerIndex, by opisaæ który prefab zosta³ spawnniêty
                Debug.Log($"Spawned: player={player} Prefab={(prefab.Equals(_operatorPrefab) ? "Operator" : "Dino")} Id={networkPlayerObject.Id} InputAuthority={networkPlayerObject.InputAuthority} HasStateAuthority={networkPlayerObject.HasStateAuthority}");
            }
            else
            {
                Debug.LogError($"Spawn failed for player={player}");
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
            Debug.Log($"OnPlayerLeft: despawned player={player}");
        }
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        Debug.Log($"OnReliableDataProgress: player={player} key={key} progress={progress}");
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        Debug.Log($"OnReliableDataReceived: player={player} key={key} len={data.Count}");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("OnSceneLoadDone");
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("OnSceneLoadStart");
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"OnSessionListUpdated: count={sessionList?.Count}");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"OnShutdown: reason={shutdownReason}");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        Debug.Log("OnUserSimulationMessage");
    }

    void Start() { }

    void Update() { }

    private NetworkRunner _runner;

    async void StartGame(GameMode mode)
    {
        // Create the Fusion runner
        _runner = gameObject.AddComponent<NetworkRunner>();

        // Only provide input on client/host (not on a dedicated server instance)
        _runner.ProvideInput = (mode == GameMode.Client || mode == GameMode.Host);

        // Register this object as callbacks provider so Fusion calls your INetworkRunnerCallbacks
        _runner.AddCallbacks(this);

        // Create the NetworkSceneInfo from the current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // Start or join a session
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
            {
                StartGame(GameMode.Host);
            }
            if (GUI.Button(new Rect(0, 40, 200, 40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
    }
}
