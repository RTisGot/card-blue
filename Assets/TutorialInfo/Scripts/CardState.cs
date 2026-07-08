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
    public ulong ownerClientId;
    public bool isBroken; // 

   
    public CardState(int x, int y, CardType cardType, bool rotated, ulong ownerClientId, bool isBroken)
    {
        this.x = x;
        this.y = y;
        this.cardType = cardType;
        this.rotated = rotated;
        this.ownerClientId = ownerClientId;
        this.isBroken = isBroken; 
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);

        int tempCardType = (int)cardType;
        serializer.SerializeValue(ref tempCardType);
        cardType = (CardType)tempCardType;

        serializer.SerializeValue(ref rotated);
        serializer.SerializeValue(ref ownerClientId);
        serializer.SerializeValue(ref isBroken); 
    }

    //オブジェクトの比較
    public bool Equals(CardState other)
    {
        return x == other.x
            && y == other.y
            && cardType == other.cardType
            && rotated == other.rotated
            && ownerClientId == other.ownerClientId
            && isBroken == other.isBroken; 
    }
}