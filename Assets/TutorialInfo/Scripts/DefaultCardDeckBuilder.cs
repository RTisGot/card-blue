using System.Collections.Generic;

public interface ICardDeckBuilder
{
    void Build(ICollection<CardType> destination);
}

/// <summary>
/// Builds the default draw-deck composition. BoardManager decides when to
/// build and draw; this builder owns only the composition policy.
/// </summary>
public sealed class DefaultCardDeckBuilder : ICardDeckBuilder
{
    public void Build(ICollection<CardType> destination)
    {
        destination.Clear();

        Add(destination, CardType.URload, 3);
        Add(destination, CardType.DLload, 2);
        Add(destination, CardType.DRload, 2);
        Add(destination, CardType.ULload, 3);

        Add(destination, CardType.DLRload, 2);
        Add(destination, CardType.ULRload, 0);
        Add(destination, CardType.UDRload, 2);
        Add(destination, CardType.UDLload, 0);

        Add(destination, CardType.UDLRload, 1);
        Add(destination, CardType.LRload, 1);
        Add(destination, CardType.URload, 2);

        Add(destination, CardType.LRdeadend, 1);
        Add(destination, CardType.LDdeadend, 1);
        Add(destination, CardType.UDdeadend, 0);
        Add(destination, CardType.UDLRdeadend, 1);
        Add(destination, CardType.UDLdeadend, 0);
        Add(destination, CardType.RDdeadend, 1);
        Add(destination, CardType.Ldeadend, 1);
        Add(destination, CardType.Udeadend, 1);
        Add(destination, CardType.ULRdeadend, 1);

        Add(destination, CardType.DLloadHandkerchief, 1);
        Add(destination, CardType.DRloadPocketwatch, 1);
        Add(destination, CardType.ULRloadBucket, 1);
        Add(destination, CardType.ULRloadMouse, 1);
        Add(destination, CardType.UDLloadPot, 1);
        Add(destination, CardType.UDLloadShoe, 1);
        Add(destination, CardType.UDLRloadBone, 1);
        Add(destination, CardType.UDLRloadCup, 1);
        Add(destination, CardType.UDLRloadHat, 1);
        Add(destination, CardType.LRloadSpoon, 1);
        Add(destination, CardType.LRloadWheel, 1);
        Add(destination, CardType.UDloadBucket, 1);
        Add(destination, CardType.UDLdeadendHedgehog, 1);
        Add(destination, CardType.UDdeadendFriedegg, 1);

        Add(destination, CardType.Lanternrepaire, 2);
        Add(destination, CardType.Lanternban, 3);
        Add(destination, CardType.Pickaxerepaire, 2);
        Add(destination, CardType.Pickaxeban, 3);
        Add(destination, CardType.Railcarrepaire, 2);
        Add(destination, CardType.Railcarban, 3);
        Add(destination, CardType.Treasuremap, 6);
        Add(destination, CardType.Fallingrocks, 3);
    }

    private static void Add(ICollection<CardType> destination, CardType type, int count)
    {
        for (int i = 0; i < count; i++)
        {
            destination.Add(type);
        }
    }
}
