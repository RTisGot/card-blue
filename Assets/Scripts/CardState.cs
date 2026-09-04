using System;
using Unity.Netcode;

//
//NGOからカードの状態を同期する用の構造体
public struct CardState : INetworkSerializable, IEquatable<CardState>
{
    //カードの座標
    public int x;
    public int y;

    public CardType cardType;
    public bool rotated;
    public bool isFlipped; // カードが裏返しになっているかどうかを示すフラグ
    public ulong ownerClientId;
    public bool isLanternBroken; // 
    public bool isPickaxeBroken; // 
    public bool isRailcarBroken; //

    public CardState(int x, int y, CardType cardType, bool rotated, ulong ownerClientId, bool isLanternBroken, bool isPickaxeBroken, bool isRailcarBroken)
    {
        this.x = x;
        this.y = y;
        this.cardType = cardType;
        // ゴールだけは到達するまで裏向き。それ以外のカードは表向きで表示する。
        this.isFlipped = cardType != CardType.Goal;
        this.rotated = rotated;
        this.ownerClientId = ownerClientId;
        this.isLanternBroken = isLanternBroken; 
        this.isPickaxeBroken = isPickaxeBroken;
        this.isRailcarBroken = isRailcarBroken;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);

        int tempCardType = (int)cardType;
        serializer.SerializeValue(ref tempCardType);
        cardType = (CardType)tempCardType;
        serializer.SerializeValue(ref isFlipped);
        serializer.SerializeValue(ref rotated);
        serializer.SerializeValue(ref ownerClientId);
        serializer.SerializeValue(ref isLanternBroken);
        serializer.SerializeValue(ref isPickaxeBroken);
        serializer.SerializeValue(ref isRailcarBroken);
    }

    //オブジェクトの比較
    public bool Equals(CardState other)
    {
        return x == other.x
            && y == other.y
            && cardType == other.cardType
            && rotated == other.rotated
            && isFlipped == other.isFlipped
            && ownerClientId == other.ownerClientId
            && isLanternBroken == other.isLanternBroken
            && isPickaxeBroken == other.isPickaxeBroken
            && isRailcarBroken == other.isRailcarBroken;
    }
}