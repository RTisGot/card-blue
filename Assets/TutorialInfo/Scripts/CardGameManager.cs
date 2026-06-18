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
        connectedPlayerNames.OnListChanged += OnPlayerListChanged;

        if (IsServer)
        {
            // サーバー（ホスト）自身の名前を一度だけ追加
            string hostName = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.SavedPlayerName : "HostPlayer";
            connectedPlayerNames.Add(hostName);

         
        }
        else
        {
            
            StartCoroutine(RegisterNameDelayed());
        }

        // 4. 初期UI表示
        RefreshSeatUI();
    }

    [Rpc(SendTo.Server)]
    public void RegisterPlayerNameServerRpc(FixedString64Byties name)
    {
        if(!connectedPlayerNames.Contains(name))
        {
            connectedPlayerNames.Add(name);
   
        }
    }

    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
    {
        RefreshSeatUI();
    }

    private System.Collections.IEnumerator RegisterNameDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        string clientName = NetworkGameManager.Instance != null ? NetworkGameManager.Instance.SavedPlayerName : "GuestPlayer";
        RegisterPlayerNameServerRpc(clientName);
    }

    // サーバー側で名前を受けるRPC（最新の書き方）
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterPlayerNameServerRpc(FixedString64Bytes nameOfPlayer)
    {
        // 重複登録防止のチェック（必要であれば）
        if (!connectedPlayerNames.Contains(nameOfPlayer))
        {
            connectedPlayerNames.Add(nameOfPlayer);
            Debug.Log($"サーバー: {nameOfPlayer} を追加しました");
        }
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
            if (seatNameTexts[i] == null)
            {
             
                var found = GameObject.Find($"playerText{i}");
                if (found != null) seatNameTexts[i] = found.GetComponent<TMPro.TextMeshProUGUI>();
            }

            if (seatNameTexts[i] != null) seatNameTexts[i].text = "待機中...";

        }

        for (int i = 0; i < connectedPlayerNames.Count; i++)
        {
            if (i < seatNameTexts.Count && seatNameTexts[i] != null)
            {
                seatNameTexts[i].text = connectedPlayerNames[i].ToString();
                Debug.Log($"[UI更新] {i}番目に名前セット: {connectedPlayerNames[i]}");
            }
        }
    }

    public override void OnNetworkDespawn()

    {
        string myName = (NetworkGameManager.Instance != null)
                     ? NetworkGameManager.Instance.SavedPlayerName
                     : "Unknown";
        Debug.Log("OnNetworkSpawn で取得した名前: " + myName);

        RefreshSeatUI();

        if (!IsServer)
        {
            RegisterPlayerNameServerRpc(myName);
        }
        else
        {
            // サーバー自身は直接追加
            connectedPlayerNames.Add(myName);
        }

       
      
    }

}
