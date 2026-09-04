using System;
using Unity.Netcode;

public struct PlayerToolState : INetworkSerializable, IEquatable<PlayerToolState>
{
    public ulong clientId;
    public bool isLanternBroken;
    public bool isPickaxeBroken;
    public bool isRailcarBroken;

    public PlayerToolState(ulong clientId)
    {
        this.clientId = clientId;
        isLanternBroken = false;
        isPickaxeBroken = false;
        isRailcarBroken = false;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref isLanternBroken);
        serializer.SerializeValue(ref isPickaxeBroken);
        serializer.SerializeValue(ref isRailcarBroken);
    }

    public bool Equals(PlayerToolState other)
    {
        return clientId == other.clientId &&
               isLanternBroken == other.isLanternBroken &&
               isPickaxeBroken == other.isPickaxeBroken &&
               isRailcarBroken == other.isRailcarBroken;
    }
}
