using Gwota.Core.Data;
using Gwota.Core.Session;

namespace Gwota.Core.DeckBuilding;

/// <summary>
/// The player's own decks, kept in display order (creation order). The shipped decks seed it on first launch and are
/// then ordinary decks: editable, renamable and deletable, except that the last remaining deck cannot be deleted.
/// </summary>
public sealed class DeckCollection
{
    private const string IdPrefix = "deck_";

    private readonly List<DeckDefinition> _decks = [];

    public DeckCollection(IEnumerable<DeckDefinition> decks)
    {
        ArgumentNullException.ThrowIfNull(decks);
        foreach (var deck in decks)
        {
            if (Find(deck.Id) is null)
            {
                _decks.Add(deck);
            }
        }
    }

    public IReadOnlyList<DeckDefinition> Decks => _decks;

    public int Count => _decks.Count;

    public bool CanDelete => _decks.Count > 1;

    /// <summary>A collection holding a copy of every shipped deck (used when the player has no saved decks).</summary>
    public static DeckCollection FromDefaults(GameData data) => new(MatchFactory.PlayableDecks(data));

    public DeckDefinition? Find(string id) => _decks.FirstOrDefault(d => d.Id == id);

    /// <summary>Creates and appends an empty deck of <paramref name="faction"/> with a unique id and name and its default leader.</summary>
    public DeckDefinition Create(Faction faction, GameData data)
    {
        if (faction == Faction.Neutral)
        {
            throw new ArgumentException("A deck faction cannot be Neutral.", nameof(faction));
        }

        var deck = new DeckDefinition
        {
            Id = UniqueId(),
            Name = UniqueName($"New {FactionInfo.ShortName(faction)} deck"),
            Faction = faction,
            LeaderId = DeckEditor.DefaultLeader(data, faction).Id,
            Cards = [],
        };
        _decks.Add(deck);
        return deck;
    }

    /// <summary>Replaces the deck with the same id in place, or appends it if it is new.</summary>
    public void Save(DeckDefinition deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var index = _decks.FindIndex(d => d.Id == deck.Id);
        if (index >= 0)
        {
            _decks[index] = deck;
        }
        else
        {
            _decks.Add(deck);
        }
    }

    /// <summary>Deletes a deck; refused (false) for unknown ids and for the last remaining deck.</summary>
    public bool Delete(string id)
    {
        if (!CanDelete)
        {
            return false;
        }

        return _decks.RemoveAll(d => d.Id == id) > 0;
    }

    private string UniqueId()
    {
        var n = 1;
        while (Find(IdPrefix + n.ToString(System.Globalization.CultureInfo.InvariantCulture)) is not null)
        {
            n++;
        }

        return IdPrefix + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string UniqueName(string baseName)
    {
        var name = baseName;
        for (var n = 2; _decks.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)); n++)
        {
            name = $"{baseName} {n.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        return name;
    }
}
