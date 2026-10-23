using Gwota.Core.Data;

namespace Gwota.Core.DeckBuilding;

/// <summary>How the player's decks were obtained when the game started.</summary>
public enum DeckLoadStatus
{
    /// <summary>No saved decks yet: the collection is seeded with the shipped decks.</summary>
    Seeded,

    /// <summary>The saved decks were read (possibly cleaned up, see <see cref="DeckCollectionSerializer"/>).</summary>
    Loaded,

    /// <summary>The saved file held nothing usable: the shipped decks replace it, and the caller should keep a backup.</summary>
    Recovered,
}

public sealed record DeckLoadResult(DeckCollection Collection, DeckLoadStatus Status)
{
    /// <summary>Reads the saved decks, falling back to the shipped decks when there are none or none are usable.</summary>
    public static DeckLoadResult From(string? savedJson, GameData data, DeckRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (string.IsNullOrWhiteSpace(savedJson))
        {
            return new DeckLoadResult(DeckCollection.FromDefaults(data), DeckLoadStatus.Seeded);
        }

        return DeckCollectionSerializer.Deserialize(savedJson, data, rules) is { } saved
            ? new DeckLoadResult(saved, DeckLoadStatus.Loaded)
            : new DeckLoadResult(DeckCollection.FromDefaults(data), DeckLoadStatus.Recovered);
    }
}
