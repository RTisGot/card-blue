using System.Collections.Generic;
using UnityEngine;

/// <summary>
//カードがスタートカードに接続されているかどうかを判定する
public interface IBoardConnectivityService
{

    bool ConnectsToStart(
        Vector2Int position,
        CardType cardType,
        bool rotated,
        IEnumerable<CardState> placedCards);

    //繋がっている地形がスタートカードに接続されているか判定
    bool ExistingCardConnectsToStart(
        Vector2Int startPosition,
        IEnumerable<CardState> placedCards);
}

/// <summary>
/// Performs graph traversal and road-edge matching for the board.
/// This service has no scene or networking dependencies, so board rules can be
/// tested independently from BoardManager.
/// </summary>
public sealed class BoardConnectivityService : IBoardConnectivityService
{
    //4方向用のベクトル配列(中身の変換無し)
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    //今おこうとしているカードがスタートカードに接続されているか判定
    public bool ConnectsToStart(
        Vector2Int position,
        CardType cardType,
        bool rotated,
        IEnumerable<CardState> placedCards)
    {
        //座標のデータ構造から辞書にコピーする
        Dictionary<Vector2Int, CardState> board = CreateSnapshot(placedCards);

        foreach (Vector2Int direction in Directions)//Directionsの4方向をループ
        {
            Vector2Int neighborPosition = position + direction;//隣のマスを計算
            //隣のマスにカードが置かれていない場合はスキップ(次のマスに)
            if (!board.TryGetValue(neighborPosition, out CardState neighbor) ||
                !HasRoadConnection(cardType, rotated, direction, neighbor))
            {
                continue;
            }
            //隣のマスがスタートカードか、隣のマスからスタートカードに接続されている場合はtrueを返す
            if (neighbor.cardType == CardType.Start ||
                (!CardRules.IsDeadEnd(neighbor.cardType) &&
                 ExistingCardConnectsToStart(neighborPosition, board)))
            {
                return true;
            }
        }

        return false;
    }

    //盤面に置かれているカードの情報を辞書に変換する
    public bool ExistingCardConnectsToStart(
        Vector2Int startPosition,
        IEnumerable<CardState> placedCards)//盤面に置かれているカードの情報を受け取る
    {
        return ExistingCardConnectsToStart(startPosition, CreateSnapshot(placedCards));
    }

    //探索する(BFS)
    private static bool ExistingCardConnectsToStart(
        Vector2Int startPosition,
        IReadOnlyDictionary<Vector2Int, CardState> board)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();　　　//見つけた場所から探索
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();//見つけた場所の座標を記録する
        queue.Enqueue(startPosition);　　　　　　　　　　       //探索開始座標を追加
        visited.Add(startPosition);                             //探索開始座標を記録

        //queueに調べる座標がある限りループ(startまで繋がっているか)
        while (queue.Count > 0)
        {
            Vector2Int currentPosition = queue.Dequeue();//現在の座標を取得
            //現在の座標にカードが置かれていない場合は次へ
            if (!board.TryGetValue(currentPosition, out CardState currentCard))
            {
                continue;
            }
            //現在のカードがスタートカードの場合はtrueを返す
            if (currentCard.cardType == CardType.Start)
            {
                return true;
            }

            // 行き止まりカードは見た目に通路があっても、Goalへ到達する経路には使用しない。
            if (CardRules.IsDeadEnd(currentCard.cardType))
            {
                continue;
            }

            foreach (Vector2Int direction in Directions)//4方向をループ
            {
                Vector2Int nextPosition = currentPosition + direction;//隣のマスの座標を計算
                if (visited.Contains(nextPosition) ||                                                   //既に調べた場所
                    !board.TryGetValue(nextPosition, out CardState nextCard) ||                         //隣のマスにカードが置かれていない
                    CardRules.IsDeadEnd(nextCard.cardType) ||                                           //行き止まりは経路として通過しない
                    !HasRoadConnection(currentCard.cardType, currentCard.rotated, direction, nextCard))//隣のマスと現在のマスが繋がっていない
                {
                    continue;
                }

                visited.Add(nextPosition);//次の場所を追加
                queue.Enqueue(nextPosition);
            }
        }

        return false;
    }

    private static Dictionary<Vector2Int, CardState> CreateSnapshot(
        IEnumerable<CardState> placedCards)
    {
        Dictionary<Vector2Int, CardState> board = new Dictionary<Vector2Int, CardState>();
        foreach (CardState card in placedCards)
        {
            board[new Vector2Int(card.x, card.y)] = card;
        }

        return board;
    }

    public static bool HasRoadConnection(
        CardType cardType,
        bool rotated,
        Vector2Int direction,
        CardState neighbor)
    {
        PathDirection cardPaths = CardRules.GetRotatedPaths(cardType, rotated);
        PathDirection neighborPaths = CardRules.GetRotatedPaths(neighbor.cardType, neighbor.rotated);
        PathDirection cardDirection = GetPathDirection(direction);
        PathDirection neighborDirection = GetOppositePathDirection(direction);

        return (cardPaths & cardDirection) != 0 &&
               (neighborPaths & neighborDirection) != 0;
    }

    private static PathDirection GetPathDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return PathDirection.Up;
        if (direction == Vector2Int.down) return PathDirection.Down;
        if (direction == Vector2Int.left) return PathDirection.Left;
        return PathDirection.Right;
    }

    private static PathDirection GetOppositePathDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return PathDirection.Down;
        if (direction == Vector2Int.down) return PathDirection.Up;
        if (direction == Vector2Int.left) return PathDirection.Right;
        return PathDirection.Left;
    }
}
