using TMPro;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class RelayManager : MonoBehaviour
{

    [Header("UI設定")]
    [SerializeField] private TMP_InputField joinCodeInput; // インスペクターから入力欄をドラッグ
    [SerializeField] private TMP_Text joinCodeText;       // インスペクターから表示用テキストをドラッグ

    // ボタンに割り当てる「ホスト開始」関数
    public async void OnClick_StartHost()
    {
        // 既存の StartHostWithRelay を呼び出す
        string code = await StartHostWithRelay();

        // 取得したコードをUIに表示
        if (joinCodeText != null) joinCodeText.text = "Code: " + code;
        Debug.Log("部屋を作成しました: " + code);
    }

    // ボタンに割り当てる「参加」関数
    public async void OnClick_Join()
    {
        string code = joinCodeInput.text;
        if (string.IsNullOrEmpty(code)) return;

        await JoinWithRelay(code);
        Debug.Log("参加を試みます: " + code);
    }

    async void Awake()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    public async Task<string> StartHostWithRelay()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        // ★コンストラクタではなく、直接 SetRelayServerData に必要な情報を渡すメソッドを使います
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );

        NetworkManager.Singleton.StartHost();
        return joinCode;
    }

    public async Task JoinWithRelay(string joinCode)
    {
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // ★クライアント用も同様にセット
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetRelayServerData(
            joinAllocation.RelayServer.IpV4,
            (ushort)joinAllocation.RelayServer.Port,
            joinAllocation.AllocationIdBytes,
            joinAllocation.Key,
            joinAllocation.ConnectionData,
            joinAllocation.HostConnectionData
        );

        NetworkManager.Singleton.StartClient();
    }
}