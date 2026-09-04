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
            CardType.Goal => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.GoalGold => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.GoalEmpty => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.GoalEmptyTop => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.GoalEmptyMiddle => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.GoalEmptyBottom => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
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

            // 小物類有り
            CardType.DLloadHandkerchief => PathDirection.Down | PathDirection.Left,
            CardType.DRloadPocketwatch => PathDirection.Down | PathDirection.Right,
            CardType.ULRloadBucket => PathDirection.Up | PathDirection.Left | PathDirection.Right,
            CardType.ULRloadMouse => PathDirection.Up | PathDirection.Left | PathDirection.Right,
            CardType.UDLloadPot => PathDirection.Up | PathDirection.Down | PathDirection.Left,
            CardType.UDLloadShoe => PathDirection.Up | PathDirection.Down | PathDirection.Left,
            CardType.UDLRloadBone => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.UDLRloadCup => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.UDLRloadHat => PathDirection.Up | PathDirection.Down | PathDirection.Left | PathDirection.Right,
            CardType.LRloadSpoon => PathDirection.Left | PathDirection.Right,
            CardType.LRloadWheel => PathDirection.Left | PathDirection.Right,
            CardType.UDloadBucket => PathDirection.Up | PathDirection.Down,
            CardType.UDLdeadendHedgehog => PathDirection.Up | PathDirection.Down | PathDirection.Left,
            CardType.UDdeadendFriedegg => PathDirection.Up | PathDirection.Down,
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

                // Goalは接続方向を固定しない。道がGoal側を向いていない辺があっても
                // ほかの隣接カードとの配置条件を満たしていれば配置を許可する。
                if (IsGoalCard(neighbor.Value.cardType))
                {
                    PathDirection directionToGoal = GetPathDirection(direction);
                    if ((newCardPaths & directionToGoal) != 0)
                    {
                        hasRoadConnection = true;
                    }

                    continue;
                }

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

        // 片方に道があり、もう片方にない道が壁に突き当たっている状態のみ拒否
        if (aHasPath != bHasPath) return false;


        return true;
    }

    public static bool HasRoadConnection(PathDirection aPaths, PathDirection aDir, PathDirection bPaths, PathDirection bDir)
    {
        return (aPaths & aDir) != 0 && (bPaths & bDir) != 0;
    }

    public static PathDirection GetPathDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return PathDirection.Up;

        if (direction == Vector2Int.down)
            return PathDirection.Down;

        if (direction == Vector2Int.left)
            return PathDirection.Left;

        if (direction == Vector2Int.right)
            return PathDirection.Right;

        return PathDirection.None;
    }

    public static PathDirection GetOppositePathDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return PathDirection.Down;

        if (direction == Vector2Int.down)
            return PathDirection.Up;

        if (direction == Vector2Int.left)
            return PathDirection.Right;

        if (direction == Vector2Int.right)
            return PathDirection.Left;

        return PathDirection.None;
    }

    public static bool IsDeadEnd(CardType type)
    {
        return type switch
        {
            CardType.UDLdeadend => true,
            CardType.ULRdeadend => true,
            CardType.RDdeadend => true,
            CardType.LDdeadend => true,
            CardType.LRdeadend => true,
            CardType.Ldeadend => true,
            CardType.UDdeadend => true,
            CardType.Udeadend => true,
            CardType.UDLRdeadend => true,
            CardType.UDLdeadendHedgehog => true,
            CardType.UDdeadendFriedegg => true,
            _ => false
        };
    }

    public static bool IsGoalCard(CardType type)
    {
        return type == CardType.Goal ||
               type == CardType.GoalGold ||
               type == CardType.GoalEmpty ||
               type == CardType.GoalEmptyTop ||
               type == CardType.GoalEmptyMiddle ||
               type == CardType.GoalEmptyBottom;
    }
}
