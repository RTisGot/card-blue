using UnityEngine;
using TMPro;
using Unity.Netcode;

public class PlayerDisplay : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private PlayerNetworkData networkData;//

    public override void OnNetworkSpawn()
    {
        // 値が変わったときに自動でUIを更新するように登録
        networkData.PlayerInfoVariable.OnValueChanged += (oldVal, newVal) => {
            UpdateName(newVal.playerName.ToString());
        };

        // 既に値が入っている場合に備えて初期化
        UpdateName(networkData.PlayerInfoVariable.Value.playerName.ToString());
    }

    public void UpdateName(string playerName)
    {
        if (nameText != null) nameText.text = playerName;
    }
}

