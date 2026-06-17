using Unity.Netcode;
using Unity.Collections;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class CardGameManager : NetworkBehaviour

{
    [SerializeField] private TMP_Text statusText;
    [Header("UI Slots (0 = Main Player, 1~3 = Other Players)")]

    [SerializeField] private List<TMP_Text> seatNameTexts = new List<TMP_Text>();

    // 参加者全員の名前を格納
    private NetworkList<FixedString64Bytes> connectedPlayerNames;

    //初期化
    private void Awake()

    { 
        connectedPlayerNames = new NetworkList<FixedString64Bytes>();
    }

    public override void OnNetworkSpawn()

    {

        // リストの中身が変わったときにUIを更新するイベントを登録

        connectedPlayerNames.OnListChanged += OnPlayerListChanged;

        // サーバー（Host）だけが「誰かが入ってきた時」のイベントを監視する

        if (IsServer)

        {
            // Host自身の名前を登録
            string hostName = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.SavedPlayerName : "HostPlayer";

            connectedPlayerNames.Add(hostName);

            // 新しく接続
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnect;

            // 誰かが切断時
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        }

        // サーバーへクライアントの名前を通知する

        if (!IsServer)

        {

            string clientName = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.SavedPlayerName : "GuestPlayer";

            RegisterPlayerNameServerRpc(clientName);

        }

        RefreshSeatUI();

    }

    // クライアントがサーバーに送るRPC

    [ServerRpc(RequireOwnership = false)]

    private void RegisterPlayerNameServerRpc(string nameOfPlayer)

    {

        // サーバー側でリストの末尾に名前を追加

        connectedPlayerNames.Add(nameOfPlayer);

    }

    // 新しいプレイヤーが接続したときに呼ばれる

    private void OnClientConnect(ulong clientId)

    {

        Debug.Log($"プレイヤー(ID: {clientId}) が接続しました。現在の人数: {connectedPlayerNames.Count}人");

    }

    // プレイヤーが切断したときに呼ばれる

    private void OnClientDisconnect(ulong clientId)

    {


        // 席を詰める処理（RemoveAtなど）を行います

    }

    // 同期リスト（名前の一覧）が更新されたらUIを書き換える

    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)

    {

        RefreshSeatUI();

    }

    // 各プレイヤーの画面ごとに、見え方を制御してUIをリフレッシュする

    private void RefreshSeatUI()

    {

        if(statusText != null)
        {
            if(connectedPlayerNames.Count >= 2)
                statusText.text = "準備完了！";
            else
                statusText.text = "プレイヤーを待っています...";
        }
      

        for (int i = 0; i < seatNameTexts.Count; i++)

        {

            if (seatNameTexts[i] != null) seatNameTexts[i].text = "待機中...";

        }

        // 2. 自分が何番目のプレイヤーか（インデックス）を特定する

        // 自分がHostなら0番目、Clientなら自分が送った名前の位置になります

        int myIndex = 0;

        string myName = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.SavedPlayerName : "";

        for (int i = 0; i < connectedPlayerNames.Count; i++)

        {

            if (connectedPlayerNames[i].ToString() == myName)

            {

                myIndex = i;

                break;

            }

        }

        // 3. 人数分の名前を、自分の画面から見た適切な「席」に配置する

        for (int i = 0; i < connectedPlayerNames.Count; i++)

        {

            // 自分が常に「0番目の席（手前）」に映るように、インデックスをずらす（相対位置の計算）

            int relativeSeatIndex = (i - myIndex + connectedPlayerNames.Count) % connectedPlayerNames.Count;

            // 用意されたUIの数を超えないように制御して名前を描画

            if (relativeSeatIndex < seatNameTexts.Count && seatNameTexts[relativeSeatIndex] != null)

            {

                seatNameTexts[relativeSeatIndex].text = connectedPlayerNames[i].ToString();

            }

        }

    }

    public override void OnNetworkDespawn()

    {

        connectedPlayerNames.OnListChanged -= OnPlayerListChanged;

        if (IsServer && NetworkManager.Singleton != null)

        {

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnect;

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

        }

    }

}
