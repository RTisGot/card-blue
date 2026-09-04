using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class RelayManager : MonoBehaviour
{
    private static readonly Dictionary<ulong, string> PlayerNamesByClientId =
        new Dictionary<ulong, string>();

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject hostSetupPanel;
    [SerializeField] private GameObject joinSetupPanel;
    [SerializeField] private GameObject matchingPanel;

    [Header("Host UI")]
    [SerializeField] private TMP_InputField hostPasswordInput;
    [SerializeField] private TMP_Text roomIdText;

    [Header("Join UI")]
    [SerializeField] private TMP_InputField joinRoomIdInput;
    [SerializeField] private TMP_InputField joinPasswordInput;

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;

    [Header("Matching UI")]
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private TMP_Text[] participantNameTexts;

    private const int MaxConnections = 4;
    private const string ParticipantNamesMessageName = "ParticipantNames";
    private string hostRoomPassword = "";
    private string pendingJoinRoomId = "";
    private bool isStartingConnection;

    [System.Serializable]
    public class RelayConnectionPayload
    {
        public string playerName;
        public string roomPassword;
    }

    private async void Awake() => await InitializeUnityServices();

    private void Start()
    {
        if (hostPasswordInput != null) hostPasswordInput.onSubmit.AddListener((s) => OnClick_StartHost());
        if (joinRoomIdInput != null) joinRoomIdInput.onSubmit.AddListener((s) => OnClick_Join());
        if (joinPasswordInput != null) joinPasswordInput.onSubmit.AddListener((s) => OnClick_Join());

       
    }

    private void RegisterNetworkCallbacks(bool registerApproval)
    {
        UnregisterNetworkCallbacks();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            if (registerApproval)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
            }
        }
    }

    private void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
            if (NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(
                    ParticipantNamesMessageName);
            }
        }
    }

    private void RegisterParticipantNamesMessageHandler()
    {
        if (NetworkManager.Singleton?.CustomMessagingManager == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(
            ParticipantNamesMessageName);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            ParticipantNamesMessageName,
            OnParticipantNamesMessage);
    }

    public async void OnClick_StartHost()
    {
        if (isStartingConnection) return;
        isStartingConnection = true;
        try
        {
            await InitializeUnityServices();
            if (!await EnsureSignedIn()) return;
            hostRoomPassword = hostPasswordInput?.text.Trim() ?? "";
            await ShutdownIfRunning();
            if (!TryGetTransport(out UnityTransport transport)) return;

            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections - 1);
            var roomId = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            transport.SetRelayServerData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            RegisterNetworkCallbacks(true);
            if (NetworkManager.Singleton.StartHost())
            {
                RegisterParticipantNamesMessageHandler();
                PlayerNamesByClientId.Clear();
                PlayerNamesByClientId[NetworkManager.ServerClientId] =
                    GetSavedPlayerName("Host");
                RefreshParticipantList();

                if (roomIdText != null) roomIdText.text = roomId;
                ShowMatchingPanel(roomId);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            SetStatus("Host Error: " + e.Message);
        }
        finally { isStartingConnection = false; }
    }

    public async void OnClick_Join()
    {
        if (isStartingConnection) return;

        string roomId = joinRoomIdInput?.text.Trim() ?? "";
        string password = joinPasswordInput?.text.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(roomId))
        {
            SetStatus("IDを入力してください");
            return;
        }

        isStartingConnection = true;
        try
        {
            await InitializeUnityServices();
            if (!await EnsureSignedIn())
            {
                isStartingConnection = false;
                return;
            }

            await ShutdownIfRunning();
            if (!TryGetTransport(out UnityTransport transport))
            {
                isStartingConnection = false;
                return;
            }

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(roomId);
            transport.SetRelayServerData(joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port, joinAllocation.AllocationIdBytes, joinAllocation.Key, joinAllocation.ConnectionData, joinAllocation.HostConnectionData);

            // 承認の判定はHost側だけで行う。
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

            string playerName = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.SavedPlayerName
                : "Guest";
            var payload = new RelayConnectionPayload { playerName = playerName, roomPassword = password };
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            PlayerNamesByClientId.Clear();

            RegisterNetworkCallbacks(false);
            pendingJoinRoomId = roomId;

            if (!NetworkManager.Singleton.StartClient())
            {
                SetStatus("接続を開始できませんでした");
                isStartingConnection = false;
            }
            else
            {
                RegisterParticipantNamesMessageHandler();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            SetStatus("Join Error: " + e.Message);
            isStartingConnection = false;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        res.CreatePlayerObject = true;
        res.Pending = false;

        if (req.ClientNetworkId == NetworkManager.ServerClientId)
        {
            res.Approved = true;
            return;
        }

        RelayConnectionPayload payload = null;
        try
        {
            if (req.Payload != null && req.Payload.Length > 0)
            {
                payload = JsonUtility.FromJson<RelayConnectionPayload>(
                    Encoding.UTF8.GetString(req.Payload));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"接続情報を読み取れませんでした: {e.Message}");
        }

        res.Approved = payload != null && payload.roomPassword == hostRoomPassword;
        if (!res.Approved)
        {
            res.Reason = "部屋のパスワードが違います。";
            return;
        }

        string approvedName = payload.playerName?.Trim();
        PlayerNamesByClientId[req.ClientNetworkId] =
            string.IsNullOrWhiteSpace(approvedName)
                ? $"Player {req.ClientNetworkId}"
                : approvedName;
        RefreshParticipantList();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            clientId != NetworkManager.ServerClientId)
        {
            foreach (ulong registeredClientId in PlayerNamesByClientId.Keys)
            {
                SyncApprovedNameToPlayerObject(registeredClientId, true);
            }

            StartCoroutine(BroadcastParticipantNamesWhenReady(clientId));
            SendParticipantNamesToClient(clientId);
        }

        RefreshParticipantList();

        if (clientId == NetworkManager.Singleton.LocalClientId && !NetworkManager.Singleton.IsHost)
        {
            // 参加したルームIDをUIにセット
            if (roomIdText != null)
            {
                roomIdText.text = pendingJoinRoomId;
            }

            SetStatus("Joined");
            isStartingConnection = false;

            ShowMatchingPanel(pendingJoinRoomId);
        }
    }

    private static void SendParticipantNamesToClient(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp);
        writer.WriteValueSafe(PlayerNamesByClientId.Count);
        foreach (KeyValuePair<ulong, string> entry in PlayerNamesByClientId)
        {
            writer.WriteValueSafe(entry.Key);
            FixedString64Bytes playerName = entry.Value;
            writer.WriteValueSafe(playerName);
        }

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            ParticipantNamesMessageName,
            clientId,
            writer);
    }

    private void OnParticipantNamesMessage(
        ulong senderClientId,
        FastBufferReader reader)
    {
        reader.ReadValueSafe(out int playerCount);
        for (int i = 0; i < playerCount; i++)
        {
            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out FixedString64Bytes playerName);
            ReceiveSyncedPlayerName(clientId, playerName.ToString());
        }

        RefreshParticipantList();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        PlayerNamesByClientId.Remove(clientId);
        RefreshParticipantList();

        if (NetworkManager.Singleton == null ||
            clientId != NetworkManager.Singleton.LocalClientId ||
            NetworkManager.Singleton.IsHost)
        {
            return;
        }

        string reason = NetworkManager.Singleton.DisconnectReason;
        SetStatus(string.IsNullOrWhiteSpace(reason)
            ? "部屋に接続できませんでした。HostとClientを同じ最新版にしてください。"
            : "接続失敗: " + reason);
        isStartingConnection = false;
    }

    public static bool TryGetPlayerName(ulong clientId, out string playerName)
    {
        return PlayerNamesByClientId.TryGetValue(clientId, out playerName);
    }

    public static void ReceiveSyncedPlayerName(ulong clientId, string playerName)
    {
        string safeName = playerName?.Trim();
        if (!string.IsNullOrWhiteSpace(safeName))
        {
            PlayerNamesByClientId[clientId] = safeName;
        }
    }

    private IEnumerator BroadcastParticipantNamesWhenReady(ulong joinedClientId)
    {
        const int maxWaitFrames = 120;
        for (int frame = 0; frame < maxWaitFrames; frame++)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsServer &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(
                    joinedClientId,
                    out NetworkClient joinedClient) &&
                joinedClient.PlayerObject != null &&
                joinedClient.PlayerObject.TryGetComponent(
                    out PlayerNetworkData broadcaster))
            {
                foreach (KeyValuePair<ulong, string> entry in
                         new List<KeyValuePair<ulong, string>>(PlayerNamesByClientId))
                {
                    broadcaster.BroadcastPlayerNameOnServer(entry.Key, entry.Value);
                }

                yield break;
            }

            yield return null;
        }
    }

    private static string GetSavedPlayerName(string fallback)
    {
        string savedName = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.SavedPlayerName.Trim()
            : string.Empty;

        return string.IsNullOrWhiteSpace(savedName) ? fallback : savedName;
    }

    private async Task ShutdownIfRunning()
    {
        if (NetworkManager.Singleton == null) return;

        
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsConnectedClient)
        {
            try
            {
               
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                  
                    transport.DisconnectLocalClient();
                }

                // その後にManagerを停止
                NetworkManager.Singleton.Shutdown();

                // 完了まで待機
                await Task.Delay(200);
            }
            catch (System.Exception e)
            {
                // ここまで来てもエラーが出る場合は、完全に無視する
                Debug.Log($"[RelayManager] 安全なシャットダウン完了: {e.Message}");
            }
        }
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
    }

    private async Task<bool> EnsureSignedIn()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        return AuthenticationService.Instance.IsSignedIn;
    }

    private bool TryGetTransport(out UnityTransport transport)
    {
        transport = null;
        if (NetworkManager.Singleton == null)
        {
            SetStatus("NetworkManagerが見つかりません。");
            return false;
        }

        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("UnityTransportが見つかりません。");
            return false;
        }

        return true;
    }

    private void ShowMatchingPanel(string roomId)
    {
        lobbyPanel?.SetActive(false);
        hostSetupPanel?.SetActive(false);
        joinSetupPanel?.SetActive(false);
        matchingPanel?.SetActive(true);
        RefreshParticipantList();
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            foreach (ulong clientId in PlayerNamesByClientId.Keys)
            {
                SyncApprovedNameToPlayerObject(clientId);
            }
        }

        RefreshParticipantList();

       
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            if (startButton != null)
            {
                startButton.interactable = (connectedCount >= 2);
            }
        }
    }

    private void RefreshParticipantList()
    {
        if (participantNameTexts == null || participantNameTexts.Length == 0)
        {
            return;
        }

        Dictionary<ulong, string> namesByClientId = new Dictionary<ulong, string>();
        HashSet<ulong> participantClientIds = new HashSet<ulong>();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            participantClientIds.Add(NetworkManager.ServerClientId);
            participantClientIds.Add(NetworkManager.Singleton.LocalClientId);

            foreach (var networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject != null &&
                    networkObject.TryGetComponent(out PlayerNetworkData playerData))
                {
                    ulong clientId = networkObject.OwnerClientId;
                    participantClientIds.Add(clientId);
                    string playerName = playerData.PlayerInfoVariable.Value.playerName.ToString().Trim();
                    if (IsFinalPlayerName(playerName))
                    {
                        namesByClientId[clientId] = playerName;
                    }
                }
            }
        }

        // ホストから同期された正式名を最優先にする。
        foreach (KeyValuePair<ulong, string> entry in PlayerNamesByClientId)
        {
            participantClientIds.Add(entry.Key);
            string playerName = entry.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                namesByClientId[entry.Key] = playerName;
            }
        }

        // 自分のPlayerNetworkDataが同期される前でも、入力済みの名前を表示する。
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            string localName = GetSavedPlayerName(string.Empty);
            if (!string.IsNullOrWhiteSpace(localName))
            {
                namesByClientId[NetworkManager.Singleton.LocalClientId] = localName;
            }
        }

        if (namesByClientId.Count == 0)
        {
            string savedName = GetSavedPlayerName("Player");
            if (!string.IsNullOrWhiteSpace(savedName))
            {
                namesByClientId[0] = savedName;
            }
        }

        List<ulong> sortedClientIds = new List<ulong>(participantClientIds);
        sortedClientIds.Sort();

        for (int i = 0; i < participantNameTexts.Length; i++)
        {
            if (participantNameTexts[i] == null)
            {
                continue;
            }

            if (i >= sortedClientIds.Count)
            {
                participantNameTexts[i].text = string.Empty;
                continue;
            }

            ulong clientId = sortedClientIds[i];
            participantNameTexts[i].text = namesByClientId.TryGetValue(
                clientId,
                out string playerName)
                ? playerName
                : string.Empty;
        }
    }

    private static bool IsFinalPlayerName(string playerName)
    {
        return !string.IsNullOrWhiteSpace(playerName) &&
               playerName != "Guest" &&
               playerName != "Player";
    }

    private static void SyncApprovedNameToPlayerObject(
        ulong clientId,
        bool broadcastEvenIfUnchanged = false)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer ||
            !PlayerNamesByClientId.TryGetValue(clientId, out string playerName) ||
            !NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null ||
            !client.PlayerObject.TryGetComponent(out PlayerNetworkData playerData))
        {
            return;
        }

        playerData.SetPlayerNameOnServer(playerName, broadcastEvenIfUnchanged);
    }

    public void OnClick_StartGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            // ネットワーク上の全員を指定のシーンへ移動させる
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            
        }
    }
}
