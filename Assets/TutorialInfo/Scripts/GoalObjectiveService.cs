using System;
using UnityEngine;

/// <summary>
/// Owns the server-only gold location and the reveal policy for the three goals.
/// Clients only receive the resulting CardType after a goal is reached.
/// </summary>
public sealed class GoalObjectiveService
{
    private Vector2Int goldPosition;
    private int centerY;
    private bool initialized;

    public Vector2Int[] Initialize(int x, int y, int verticalSpacing, int goldIndex)
    {
        Vector2Int[] positions =
        {
            new Vector2Int(x, y - verticalSpacing),
            new Vector2Int(x, y),
            new Vector2Int(x, y + verticalSpacing)
        };

        if (goldIndex < 0 || goldIndex >= positions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(goldIndex));
        }

        centerY = y;
        goldPosition = positions[goldIndex];
        initialized = true;
        return positions;
    }

    public CardType GetRevealedCardType(Vector2Int position)
    {
        EnsureInitialized();
        if (position == goldPosition)
        {
            return CardType.GoalGold;
        }

        if (position.y > centerY)
        {
            return CardType.GoalEmptyTop;
        }

        if (position.y < centerY)
        {
            return CardType.GoalEmptyBottom;
        }

        return CardType.GoalEmptyMiddle;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
        {
            throw new InvalidOperationException("GoalObjectiveService is not initialized.");
        }
    }
}
