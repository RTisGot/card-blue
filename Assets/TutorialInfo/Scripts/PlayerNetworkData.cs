using Unity.Netcode;

public class PlayerNetworkData : NetworkBehaviour
{
    // 全員に同期されるプレイヤー情報
    public NetworkVariable<PlayerInfo> PlayerInfoVariable = new NetworkVariable<PlayerInfo>(
        new PlayerInfo(0, "Guest"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> isLanternBroken = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isPickaxeBroken = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isRailcarBroken = new NetworkVariable<bool>(false);
    public override void OnNetworkSpawn()
    {
        // 所有者であれば、保存していた名前をサーバーに送信
        if (IsOwner)
        {
            string savedName = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.SavedPlayerName
                : "Player";

            UpdatePlayerInfoServerRpc(new PlayerInfo(OwnerClientId, savedName));
        }
    }

    [ServerRpc]
    private void UpdatePlayerInfoServerRpc(PlayerInfo info)
    {
        PlayerInfoVariable.Value = info;
    }

    public void SetToolBrokenState(CardType cardType, bool isBroken)
    {
        switch (cardType)
        {
            case CardType.Lanternban:
            case CardType.Lanternrepaire:
                isLanternBroken.Value = isBroken;
                break;
            case CardType.Pickaxeban:
            case CardType.Pickaxerepaire:
                isPickaxeBroken.Value = isBroken;
                break;
            case CardType.Railcarban:
            case CardType.Railcarrepaire:
                isRailcarBroken.Value = isBroken;
                break;
        }
    }
}
