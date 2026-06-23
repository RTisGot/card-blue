using UnityEngine;
using System;
using Unity.Netcode;

[Flags]
public enum PathDirection
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8
}

public static class CardRules
{
    public static PathDirection GetPaths(CardType type)
    {
        return type switch
        {
            CardType.Start => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.PathStraight => PathDirection.Up | PathDirection.Down,
            CardType.PathCorner => PathDirection.Up | PathDirection.Right,
            CardType.PathTJunction => PathDirection.Up | PathDirection.Down | PathDirection.Right,
            CardType.PathCross => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.DeadEnd => PathDirection.Up,
            _ => PathDirection.None 
        };
    }

    public static PathDirection GetRotatedPaths(CardType type, bool rotated)
    {
        PathDirection basePaths = GetPaths(type);
        if (!rotated) return basePaths;

        // 回転している場合、ビットをずらす
        PathDirection rotatedPaths = PathDirection.None;
        if ((basePaths & PathDirection.Up) != 0) rotatedPaths |= PathDirection.Down;
        if ((basePaths & PathDirection.Down) != 0) rotatedPaths |= PathDirection.Up;
        if ((basePaths & PathDirection.Left) != 0) rotatedPaths |= PathDirection.Right;
        if ((basePaths & PathDirection.Right) != 0) rotatedPaths |= PathDirection.Left;

        return rotatedPaths;
    }

    public static bool CanPlaceCard(Vector2Int targetPos, CardType newCardType, bool rotated, NetworkList<CardState> placedCards)
    {
        

            foreach (var card in placedCards)
            {
                if (card.x == targetPos.x && card.y == targetPos.y)
                {
                    return false;
                }
            }

            // 隣接チェック接続判定
            PathDirection newCardPaths = GetRotatedPaths(newCardType, rotated);
            bool hasNeighbor = false;

            // 周囲4方向をチェック
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int neighborPos = targetPos + direction;

                //
                CardState? neighbor = null;
                foreach (var c in placedCards)
                {
                    if (c.x == neighborPos.x && c.y == neighborPos.y)
                    {
                        neighbor = c;
                        break;
                    }
                }

                if (neighbor != null)
                {
                    hasNeighbor = true;
                    PathDirection neighborPaths = GetRotatedPaths(neighbor.Value.cardType, neighbor.Value.rotated);

                    // 方向ごとの接続判定
                    if (direction == Vector2Int.up && !IsConnected(newCardPaths, PathDirection.Up, neighborPaths, PathDirection.Down)) return false;
                    if (direction == Vector2Int.down && !IsConnected(newCardPaths, PathDirection.Down, neighborPaths, PathDirection.Up)) return false;
                    if (direction == Vector2Int.left && !IsConnected(newCardPaths, PathDirection.Left, neighborPaths, PathDirection.Right)) return false;
                    if (direction == Vector2Int.right && !IsConnected(newCardPaths, PathDirection.Right, neighborPaths, PathDirection.Left)) return false;
                }
            }


            return hasNeighbor;
        
    }

    private static bool IsConnected(PathDirection aPaths, PathDirection aDir, PathDirection bPaths, PathDirection bDir)
    {
        bool aHasPath = (aPaths & aDir) != 0;
        bool bHasPath = (bPaths & bDir) != 0;
        // 両方に道がある両方に道がないならtrue
        return aHasPath == bHasPath;
    }
}