//@brief
//unityネットワーク同期通信する為
using System;
using Unity.Netcode;

[Serializable]
public struct DealtCard : INetworkSerializable, IEquatable<DealtCard>
{
    public ulong ownerClientId;
    public CardType cardType;

    public DealtCard(ulong ownerClientId, CardType cardType)
    {
        this.ownerClientId = ownerClientId;
        this.cardType = cardType;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ownerClientId);

        int cardTypeValue = (int)cardType;
        serializer.SerializeValue(ref cardTypeValue);
        cardType = (CardType)cardTypeValue;
    }

    //カードを比較して同じか確認
    public bool Equals(DealtCard other)
    {
        return ownerClientId == other.ownerClientId && cardType == other.cardType;
    }
}
