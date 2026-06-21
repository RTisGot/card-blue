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

    private const int MaxConnections = 4;
    private string hostRoomPassword = string.Empty;
    private string pendingJoinRoomId = string.Empty;
    private bool isStartingConnection;

    private async void Awake()
    {
        await InitializeUnityServices();
    }

    private void Start()
    {
        if (hostPasswordInput != null)
        {
            hostPasswordInput.onSubmit.AddListener(_ => OnClick_StartHost());
        }

        if (joinPasswordInput != null)
        {
            joinPasswordInput.onSubmit.AddListener(_ => OnClick_Join());
        }
    }

    public async void OnClick_StartHost()
    {
        await StartHostWithRelay();
    }

    public async void OnClick_Join()
    {
        string roomId = joinRoomIdInput != null ? joinRoomIdInput.text.Trim() : string.Empty;
        string password = joinPasswordInput != null ? joinPasswordInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(roomId))
        {
            SetStatus("Enter a room ID.");
            return;
        }

        await JoinWithRelay(roomId, password);
    }

    public async Task<string> StartHostWithRelay()
    {
        if (isStartingConnection)
        {
            return string.Empty;
        }

        isStartingConnection = true;
        await InitializeUnityServices();

        string playerName = GetSavedPlayerName("HostPlayer");
        hostRoomPassword = hostPasswordInput != null ? hostPasswordInput.text : string.Empty;

        NetworkGameManager.Instance.SavedPlayerName = playerName;
        NetworkGameManager.Instance.CurrentRoomPassword = hostRoomPassword;

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections - 1);
        string roomId = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        NetworkGameManager.Instance.CurrentRoomId = roomId;

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData);

        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

        if (NetworkManager.Singleton.StartHost())
        {
            ShowMatchingPanel(roomId);
            SetStatus("Waiting for players...");
        }
        else
        {
            SetStatus("Failed to start host.");
            isStartingConnection = false;
        }

        return roomId;
    }

    public async Task JoinWithRelay(string roomId, string roomPassword)
    {
        if (isStartingConnection)
        {
            return;
        }

        isStartingConnection = true;
        await InitializeUnityServices();

        string playerName = GetSavedPlayerName("GuestPlayer");
        NetworkGameManager.Instance.SavedPlayerName = playerName;
        NetworkGameManager.Instance.CurrentRoomId = roomId;
        NetworkGameManager.Instance.CurrentRoomPassword = roomPassword;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(roomId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData);

            RelayConnectionPayload payload = new RelayConnectionPayload
            {
                playerName = playerName,
                roomPassword = roomPassword
            };

            string json = JsonUtility.ToJson(payload);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(json);

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            if (NetworkManager.Singleton.StartClient())
            {
                pendingJoinRoomId = roomId;
                SetStatus("Connecting...");
            }
            else
            {
                SetStatus("Failed to start client.");
                isStartingConnection = false;
            }
        }
        catch (RelayServiceException exception)
        {
            SetStatus($"Could not join room: {exception.Message}");
            isStartingConnection = false;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = false;
        response.CreatePlayerObject = true;
        response.Pending = false;

        if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
        {
            response.Approved = true;
            return;
        }

        string json = Encoding.UTF8.GetString(request.Payload);
        RelayConnectionPayload payload = JsonUtility.FromJson<RelayConnectionPayload>(json);

        if (payload.roomPassword == hostRoomPassword)
        {
            response.Approved = true;
            return;
        }

        response.Reason = "Room password does not match.";
        Debug.LogWarning($"Rejected client {request.ClientNetworkId}: room password does not match.");
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            pendingJoinRoomId = string.Empty;
            isStartingConnection = false;
            SetStatus("Disconnected. Check the room ID and password.");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsHost && !string.IsNullOrEmpty(pendingJoinRoomId))
        {
            ShowMatchingPanel(pendingJoinRoomId);
            SetStatus("Matching...");
            pendingJoinRoomId = string.Empty;
            isStartingConnection = false;
        }
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private void ShowMatchingPanel(string roomId)
    {
        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(false);
        }

        if (hostSetupPanel != null)
        {
            hostSetupPanel.SetActive(false);
        }

        if (joinSetupPanel != null)
        {
            joinSetupPanel.SetActive(false);
        }

        if (matchingPanel != null)
        {
            matchingPanel.SetActive(true);
        }

        if (roomIdText != null)
        {
            roomIdText.text = $"Room ID: {roomId}";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log(message);
    }

    private static string GetSavedPlayerName(string fallback)
    {
        if (NetworkGameManager.Instance == null || string.IsNullOrWhiteSpace(NetworkGameManager.Instance.SavedPlayerName))
        {
            return fallback;
        }

        return NetworkGameManager.Instance.SavedPlayerName.Trim();
    }
}
