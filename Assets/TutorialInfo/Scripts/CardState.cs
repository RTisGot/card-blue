using System;
using Unity.Netcode;

//
//NGOからカードの状態を同期する用の構造体
[Serializable]
public struct CardState : INetworkSerializable, IEquatable<CardState>//データの形式の送信,データの状態の変化
{
    public int x;
    public int y;
    public CardType cardType;
    public bool rotated;
    public ulong ownerClientId; //playerIDの保存

    //カード生成(中身の情報)
    public CardState(int x, int y, CardType cardType, bool rotated, ulong ownerClientId)
    {
        //外部から保存先に
        this.x = x;
        this.y = y;
        this.cardType = cardType;
        this.rotated = rotated;
        this.ownerClientId = ownerClientId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        //バイト型に変換して,パケットとして変換
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