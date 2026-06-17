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
    private NetworkList<FixedString64Bytes> connectedPlayerNames = new NetworkList<FixedString64Bytes>();



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

        for (int i = 0; i < connectedPlayerNames.Count; i++)
        {
            if (i < seatNameTexts.Count && seatNameTexts[i] != null)
            {
                seatNameTexts[i].text = connectedPlayerNames[i].ToString();
            }
        }
    }

    public override void OnNetworkDespawn()

    {
        string myName = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.SavedPlayerName : "NULL";
        Debug.Log("OnNetworkSpawn で取得した名前: " + myName);

        connectedPlayerNames.OnListChanged -= OnPlayerListChanged;

        if (IsServer && NetworkManager.Singleton != null)

        {

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnect;

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

        }

    }

}
