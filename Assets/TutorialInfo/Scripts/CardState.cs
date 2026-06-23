using System;
using Unity.Netcode;


[Serializable]
public struct CardState : INetworkSerializable, IEquatable<CardState>
{
    public int x;
    public int y;
    public CardType cardType;
    public bool rotated;
    public ulong ownerClientId;
    public CardState(int x, int y, CardType cardType, bool rotated, ulong ownerClientId)
    {
        this.x = x;
        this.y = y;
        this.cardType = cardType;
        this.rotated = rotated;
        this.ownerClientId = ownerClientId;
    }
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);

        // enumをintとして扱う
        int tempCardType = (int)cardType;
        serializer.SerializeValue(ref tempCardType);
        cardType = (CardType)tempCardType;

        serializer.SerializeValue(ref rotated);
        serializer.SerializeValue(ref ownerClientId);
    }

    //オブジェクト比較(等しい)
    public bool Equals(CardState other)
    {
        return x == other.x
            && y == other.y
            && cardType == other.cardType
            && rotated == other.rotated
            && ownerClientId == other.ownerClientId;
    }
}