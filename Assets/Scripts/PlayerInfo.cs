using System;
using Unity.Collections;
using Unity.Netcode;

[Serializable]
public struct PlayerInfo : INetworkSerializable, IEquatable<PlayerInfo>
{
    public ulong clientId;
    public FixedString64Bytes playerName;

    public PlayerInfo(ulong clientId, FixedString64Bytes playerName)
    {
        this.clientId = clientId;
        this.playerName = playerName;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerName);
    }

    public bool Equals(PlayerInfo other)
    {
        return clientId == other.clientId && playerName.Equals(other.playerName);
    }
}
