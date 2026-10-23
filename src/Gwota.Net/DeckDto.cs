namespace Gwota.Net;

/// <summary>A deck in wire form; the id stays local to each player's collection.</summary>
public sealed record DeckDto(string Name, Faction Faction, string LeaderId, IReadOnlyList<DeckEntry> Cards)
{
    public static DeckDto From(DeckDefinition deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return new(deck.Name, deck.Faction, deck.LeaderId, [.. deck.Cards]);
    }

    public DeckDefinition ToDefinition(string id) => new()
    {
        Id = id,
        Name = Name,
        Faction = Faction,
        LeaderId = LeaderId,
        Cards = Cards ?? [],
    };
}