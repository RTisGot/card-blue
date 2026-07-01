using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
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

    private const int MaxConnections = 4;
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
        }
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
            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections - 1);
            var roomId = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            RegisterNetworkCallbacks(true);
            if (NetworkManager.Singleton.StartHost())
            {
                PlayerNamesByClientId.Clear();
                PlayerNamesByClientId[NetworkManager.ServerClientId] =
                    GetSavedPlayerName("Host");

                if (roomIdText != null) roomIdText.text = roomId;
                ShowMatchingPanel(roomId);
            }
        }
        catch (System.Exception e) { SetStatus("Host Error: " + e.Message); }
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
            if (!await EnsureSignedIn()) return;
            await ShutdownIfRunning();

            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(roomId);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port, joinAllocation.AllocationIdBytes, joinAllocation.Key, joinAllocation.ConnectionData, joinAllocation.HostConnectionData);

            // 承認の判定はHost側だけで行う。
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

            string playerName = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.SavedPlayerName
                : "Guest";
            var payload = new RelayConnectionPayload { playerName = playerName, roomPassword = password };
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            RegisterNetworkCallbacks(false);
            pendingJoinRoomId = roomId;

            if (!NetworkManager.Singleton.StartClient())
            {
                SetStatus("接続を開始できませんでした");
                isStartingConnection = false;
            }
        }
        catch (System.Exception e) { SetStatus("Join Error: " + e.Message); isStartingConnection = false; }
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
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId && !NetworkManager.Singleton.IsHost)
        {
            // 参加したルームIDをUIにセット
            if (roomIdText != null)
            {
                roomIdText.text = pendingJoinRoomId;
            }

            ShowMatchingPanel(pendingJoinRoomId);
            SetStatus("Joined");
            isStartingConnection = false;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            PlayerNamesByClientId.Remove(clientId);
        }

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
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        return AuthenticationService.Instance.IsSignedIn;
    }

    private void ShowMatchingPanel(string roomId)
    {
        lobbyPanel?.SetActive(false);
        hostSetupPanel?.SetActive(false);
        joinSetupPanel?.SetActive(false);
        matchingPanel?.SetActive(true);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void Update()
    {
       
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            startButton.interactable = (connectedCount >= 2);
        }
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
