using System.Collections.Generic;
using UnityEngine;

public interface IBoardConnectivityService
{
    bool ConnectsToStart(
        Vector2Int position,
        CardType cardType,
        bool rotated,
        IEnumerable<CardState> placedCards);

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
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public bool ConnectsToStart(
        Vector2Int position,
        CardType cardType,
        bool rotated,
        IEnumerable<CardState> placedCards)
    {
        Dictionary<Vector2Int, CardState> board = CreateSnapshot(placedCards);

        foreach (Vector2Int direction in Directions)
        {
            Vector2Int neighborPosition = position + direction;
            if (!board.TryGetValue(neighborPosition, out CardState neighbor) ||
                !HasRoadConnection(cardType, rotated, direction, neighbor))
            {
                continue;
            }

            if (neighbor.cardType == CardType.Start ||
                ExistingCardConnectsToStart(neighborPosition, board))
            {
                return true;
            }
        }

        return false;
    }

    public bool ExistingCardConnectsToStart(
        Vector2Int startPosition,
        IEnumerable<CardState> placedCards)
    {
        return ExistingCardConnectsToStart(startPosition, CreateSnapshot(placedCards));
    }

    private static bool ExistingCardConnectsToStart(
        Vector2Int startPosition,
        IReadOnlyDictionary<Vector2Int, CardState> board)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(startPosition);
        visited.Add(startPosition);

        while (queue.Count > 0)
        {
            Vector2Int currentPosition = queue.Dequeue();
            if (!board.TryGetValue(currentPosition, out CardState currentCard))
            {
                continue;
            }

            if (currentCard.cardType == CardType.Start)
            {
                return true;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int nextPosition = currentPosition + direction;
                if (visited.Contains(nextPosition) ||
                    !board.TryGetValue(nextPosition, out CardState nextCard) ||
                    !HasRoadConnection(currentCard.cardType, currentCard.rotated, direction, nextCard))
                {
                    continue;
                }

                visited.Add(nextPosition);
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

    private static bool HasRoadConnection(
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
