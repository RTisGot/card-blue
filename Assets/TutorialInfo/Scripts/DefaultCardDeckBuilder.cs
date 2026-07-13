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
        Add(destination, CardType.DLload, 3);
        Add(destination, CardType.DRload, 3);
        Add(destination, CardType.ULload, 3);

        Add(destination, CardType.DLRload, 2);
        Add(destination, CardType.ULRload, 4);
        Add(destination, CardType.UDRload, 2);
        Add(destination, CardType.UDLload, 2);

        Add(destination, CardType.UDLRload, 4);
        Add(destination, CardType.LRload, 3);
        Add(destination, CardType.URload, 3);

        Add(destination, CardType.LRdeadend, 1);
        Add(destination, CardType.LDdeadend, 1);
        Add(destination, CardType.UDdeadend, 1);
        Add(destination, CardType.UDLRdeadend, 1);
        Add(destination, CardType.UDLdeadend, 1);
        Add(destination, CardType.RDdeadend, 1);
        Add(destination, CardType.Ldeadend, 1);
        Add(destination, CardType.Udeadend, 1);
        Add(destination, CardType.ULRdeadend, 1);

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
