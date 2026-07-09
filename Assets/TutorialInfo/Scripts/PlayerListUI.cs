//現在どのプレイヤーが参加しているか表示する
using UnityEngine;
using TMPro;
using Unity.Netcode;


public class PlayerListUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameListText;

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        string displayString = "参加者リスト:\n";//リスト表示用の文字列初期化

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerNetworkData data))
            {
                // ここにデバッグ用ログを追加
                Debug.Log($"プレイヤー検出: ID {client.ClientId}, 名前: {data.PlayerInfoVariable.Value.playerName}");
                string name = data.PlayerInfoVariable.Value.playerName.ToString();

                // 状態を判定
                bool isAnyBroken = data.isLanternBroken.Value || data.isPickaxeBroken.Value || data.isRailcarBroken.Value;

                // アイコンタグの生成
                string iconTag = "";
                if (data.isLanternBroken.Value) iconTag = "<sprite name=\"LanternBroken\">";
                else if (data.isPickaxeBroken.Value) iconTag = "<sprite name=\"PickaxeBroken\">";
                else if (data.isRailcarBroken.Value) iconTag = "<sprite name=\"RailcarBroken\">";
                else iconTag = "<sprite name=\"Normal\">";
                displayString += $"・{iconTag} {name}\n";
            }
            else
            {
                // プレイヤーオブジェクトが見つからない場合のデバッグログ
                Debug.LogWarning($"プレイヤーオブジェクトが見つかりません: ID {client.ClientId}");
            }

        }
            nameListText.text = displayString;//リスト文字列をUIに反映
    }
}