namespace Gwota.Core.Data;

/// <summary>Card back ids: a deck names the back its cards show face down; <see cref="DefaultId"/> when it names none.</summary>
public static class CardBacks
{
    public const string DefaultId = "default";

    private static readonly Dictionary<string, string> RenamedIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aegis_seal"] = "aegis",
        ["gilded_delta"] = "gilded",
        ["crimson_filigree"] = "crimson",
    };

    /// <summary>The trimmed id (its current name if the back was renamed), or <see cref="DefaultId"/> when blank.</summary>
    public static string Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return DefaultId;
        }

        var trimmed = id.Trim();
        return RenamedIds.TryGetValue(trimmed, out var renamed) ? renamed : trimmed;
    }
}

/// <summary>How a deck's cards look face down: its back id, and its faction (which tints the default back).</summary>
public readonly record struct CardBackStyle(string Id, Faction Faction)
{
    /// <summary>The style of <paramref name="deck"/>, with a normalized id.</summary>
    public static CardBackStyle Of(DeckDefinition deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return new(CardBacks.Normalize(deck.CardBackId), deck.Faction);
    }
}
