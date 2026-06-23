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

    public bool Equals(DealtCard other)
    {
        return ownerClientId == other.ownerClientId && cardType == other.cardType;
    }
}
