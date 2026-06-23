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

    private void RegisterNetworkCallbacks()
    {
        UnregisterNetworkCallbacks();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
    }

    private void UnregisterNetworkCallbacks()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
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
            RegisterNetworkCallbacks();
            if (NetworkManager.Singleton.StartHost())
            {
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

            // ★ここに追加：クライアント側でも接続承認を有効化する
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

            var payload = new RelayConnectionPayload { playerName = "Guest", roomPassword = password };
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            RegisterNetworkCallbacks();
            pendingJoinRoomId = roomId;

            NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e) { SetStatus("Join Error: " + e.Message); isStartingConnection = false; }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        res.CreatePlayerObject = true;
        if (req.ClientNetworkId == NetworkManager.ServerClientId) { res.Approved = true; return; }
        var payload = JsonUtility.FromJson<RelayConnectionPayload>(Encoding.UTF8.GetString(req.Payload));
        res.Approved = (payload != null && payload.roomPassword == hostRoomPassword);
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
            NetworkManager.Singleton.SceneManager.LoadScene("mainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
            
        }
    }
}