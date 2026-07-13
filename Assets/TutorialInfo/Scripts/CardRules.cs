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
            // スタートカード
            CardType.Start => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            // L字
            CardType.URload => PathDirection.Up | PathDirection.Right,
            CardType.DLload => PathDirection.Down | PathDirection.Left,
            CardType.DRload => PathDirection.Down | PathDirection.Right,
            CardType.ULload => PathDirection.Up | PathDirection.Left,
            //T字
            CardType.DLRload => PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.ULRload => PathDirection.Up | PathDirection.Left | PathDirection.Right,
            CardType.UDRload => PathDirection.Up | PathDirection.Down | PathDirection.Right,
            CardType.UDLload => PathDirection.Up | PathDirection.Down | PathDirection.Left,
            // 十字路・直線
            CardType.UDLRload => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.LRload => PathDirection.Left | PathDirection.Right,
            CardType.UDload => PathDirection.Up | PathDirection.Down,
            // 行き止まり
            CardType.UDLdeadend => PathDirection.Up | PathDirection.Down | PathDirection.Left,
            CardType.ULRdeadend => PathDirection.Up | PathDirection.Left | PathDirection.Right,
            CardType.RDdeadend => PathDirection.Right | PathDirection.Down,
            CardType.LDdeadend => PathDirection.Left | PathDirection.Down,
            CardType.LRdeadend => PathDirection.Left | PathDirection.Right,
            CardType.Ldeadend => PathDirection.Left,
            CardType.UDdeadend => PathDirection.Up | PathDirection.Down,
            CardType.Udeadend => PathDirection.Up,
            CardType.UDLRdeadend => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            
            //CardType.PathStraight => PathDirection.Up | PathDirection.Down,
            //CardType.PathCorner => PathDirection.Up | PathDirection.Right,
            //CardType.PathTJunction => PathDirection.Up | PathDirection.Down | PathDirection.Right,
            //CardType.PathCross => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            //CardType.DeadEnd => PathDirection.Up,
            
            _ => PathDirection.None 
        };
    }

    public static PathDirection GetRotatedPaths(CardType type, bool rotated)
    {
        PathDirection basePaths = GetPaths(type);
        if (!rotated) return basePaths;

        // Existing rotation flag flips the card by 180 degrees.
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

            // Check road connectivity against adjacent cards.
            PathDirection newCardPaths = GetRotatedPaths(newCardType, rotated);
            bool hasNeighbor = false;
            bool hasRoadConnection = false;

            // Check four directions around the target cell.
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int neighborPos = targetPos + direction;


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

                    // Validate each directional edge.
                    if (direction == Vector2Int.up && !IsConnected(newCardPaths, PathDirection.Up, neighborPaths, PathDirection.Down)) return false;
                    if (direction == Vector2Int.down && !IsConnected(newCardPaths, PathDirection.Down, neighborPaths, PathDirection.Up)) return false;
                    if (direction == Vector2Int.left && !IsConnected(newCardPaths, PathDirection.Left, neighborPaths, PathDirection.Right)) return false;
                    if (direction == Vector2Int.right && !IsConnected(newCardPaths, PathDirection.Right, neighborPaths, PathDirection.Left)) return false;

                    if (direction == Vector2Int.up && HasRoadConnection(newCardPaths, PathDirection.Up, neighborPaths, PathDirection.Down)) hasRoadConnection = true;
                    if (direction == Vector2Int.down && HasRoadConnection(newCardPaths, PathDirection.Down, neighborPaths, PathDirection.Up)) hasRoadConnection = true;
                    if (direction == Vector2Int.left && HasRoadConnection(newCardPaths, PathDirection.Left, neighborPaths, PathDirection.Right)) hasRoadConnection = true;
                    if (direction == Vector2Int.right && HasRoadConnection(newCardPaths, PathDirection.Right, neighborPaths, PathDirection.Left)) hasRoadConnection = true;
                }
            }


            return hasNeighbor && hasRoadConnection;
        
    }

    private static bool IsConnected(PathDirection aPaths, PathDirection aDir, PathDirection bPaths, PathDirection bDir)
    {
        /*bool aHasPath = (aPaths & aDir) != 0;
        bool bHasPath = (bPaths & bDir) != 0;
        // Both sides must agree: road to road, wall to wall.
        return aHasPath == bHasPath;*/
        bool aHasPath = (aPaths & aDir) != 0;
        bool bHasPath = (bPaths & bDir) != 0;

        // 「片方に道があり、もう片方にない」という矛盾（道が壁に突き当たっている状態）のみ拒否する
        if (aHasPath != bHasPath) return false;

        // それ以外（道同士、または壁同士）はOK
        return true;
    }

    private static bool HasRoadConnection(PathDirection aPaths, PathDirection aDir, PathDirection bPaths, PathDirection bDir)
    {
        return (aPaths & aDir) != 0 && (bPaths & bDir) != 0;
    }
}
