using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerListUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameListText;

    private void Update()
    {
        if (!NetworkManager.Singleton.IsListening) return;

        string displayString = "参加者リスト:\n";

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(out PlayerNetworkData data))
            {
                displayString += $"・{data.PlayerInfoVariable.Value.playerName}\n";
            }
        }
        nameListText.text = displayString;
    }
}