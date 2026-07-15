//現在どのプレイヤーが参加しているか表示する
using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerListUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameListText;

    private void Update()
    {
        //(クラッシュ制御用)
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || nameListText == null)
        {
            return;
        }

        //参加者リスト用の文字列
        string displayString = "参加者リスト:\n";
        HashSet<ulong> displayedClientIds = new HashSet<ulong>();//同じプレイヤーが複数回表示されないようにするため

        if (BoardManager.Instance != null && BoardManager.Instance.PlayerCount > 0)
        {
            for (int i = 0; i < BoardManager.Instance.PlayerCount; i++)
            {
                if (BoardManager.Instance.TryGetPlayerInfo(i, out PlayerInfo playerInfo) &&
                    displayedClientIds.Add(playerInfo.clientId))
                {
                    bool isLanternBroken = false;
                    bool isPickaxeBroken = false;
                    bool isRailcarBroken = false;
                    BoardManager.Instance.TryGetPlayerToolBrokenState(
                        playerInfo.clientId,
                        out isLanternBroken,
                        out isPickaxeBroken,
                        out isRailcarBroken);
                    displayString += FormatPlayerLine(
                        playerInfo.playerName.ToString(),
                        playerInfo.clientId,
                        isLanternBroken,
                        isPickaxeBroken,
                        isRailcarBroken);
                }
            }

            nameListText.text = displayString;
            return;
        }

        foreach (NetworkObject networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject != null && networkObject.TryGetComponent(out PlayerNetworkData data))
            {
                PlayerInfo playerInfo = data.PlayerInfoVariable.Value;
                ulong clientId = playerInfo.clientId != 0 || networkObject.OwnerClientId == 0
                    ? playerInfo.clientId
                    : networkObject.OwnerClientId;

                if (displayedClientIds.Add(clientId))
                {
                    string playerName = playerInfo.playerName.ToString();
                    displayString += FormatPlayerLine(data, playerName, clientId);
                }
            }
        }

        nameListText.text = displayString;
    }

    private string FormatPlayerLine(
        string playerName,
        ulong clientId,
        bool isLanternBroken,
        bool isPickaxeBroken,
        bool isRailcarBroken)
    {
        if (string.IsNullOrWhiteSpace(playerName) || playerName == "Guest")
        {
            playerName = $"Player {clientId}";
        }

        string iconLine = GetActionIconLine(isLanternBroken, isPickaxeBroken, isRailcarBroken);
        return string.IsNullOrEmpty(iconLine)
            ? $"・{playerName}\n"
            : $"・{playerName}\n  {iconLine}\n";
    }

    private string FormatPlayerLine(PlayerNetworkData data, string playerName, ulong clientId)
    {
        return FormatPlayerLine(
            playerName,
            clientId,
            data.isLanternBroken.Value,
            data.isPickaxeBroken.Value,
            data.isRailcarBroken.Value);
    }

    private string GetActionIconLine(bool isLanternBroken, bool isPickaxeBroken, bool isRailcarBroken)
    {
        //状況によってアイコンを変更
        string lanternIcon = isLanternBroken ? "LanternBroken" : "LanternNormal";
        string pickaxeIcon = isPickaxeBroken ? "PickaxeBroken" : "PickaxeNormal";
        string railcarIcon = isRailcarBroken ? "RailcarBroken" : "RailcarNormal";

        //一括で文字列を組み立て
        return $"<sprite name=\"{lanternIcon}\"> <sprite name=\"{pickaxeIcon}\"> <sprite name=\"{railcarIcon}\">";
    }
}
