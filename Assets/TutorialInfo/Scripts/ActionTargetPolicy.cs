/// <summary>
/// Centralizes action-card target rules so UI validation and server validation
/// always make the same decision.
/// </summary>
public sealed class ActionTargetPolicy
{
    public bool IsValidTarget(CardType cardType, ulong senderClientId, ulong targetClientId)
    {
        return !RequiresOtherPlayer(cardType) || senderClientId != targetClientId;
    }

    public bool RequiresOtherPlayer(CardType cardType)
    {
        return cardType == CardType.ActionSabotage ||
               cardType == CardType.Lanternban ||
               cardType == CardType.Pickaxeban ||
               cardType == CardType.Railcarban;
    }
}
