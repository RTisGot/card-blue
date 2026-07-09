//現在どのプレイヤーが参加しているか表示する
using UnityEngine;
using TMPro;
using Unity.Netcode;


public class PlayerListUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameListText;

    private void Update()
    {
        if (!NetworkManager.Singleton.IsListening) return;

        string displayString = "参加者リスト:\n";//リスト表示用の文字列初期化

        //接続されているクライアントの情報を取得して表示
        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            //
            if (client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(out PlayerNetworkData data))
            {
                displayString += $"・{data.PlayerInfoVariable.Value.playerName}\n";//データからplayer名を取得して表示
            }
        }
        nameListText.text = displayString;//リスト文字列をUIに反映
    }
}