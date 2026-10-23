namespace Gwota.Core.Data;

public static class DeckValidator
{
    /// <summary>Returns every rule the deck breaks (empty when the deck is legal).</summary>
    public static IReadOnlyList<string> Validate(
        DeckDefinition deck,
        IReadOnlyDictionary<string, CardDefinition> cards,
        IReadOnlyDictionary<string, LeaderDefinition> leaders,
        DeckRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(leaders);
        rules ??= DeckRules.Classic;

        var errors = new List<string>();

        if (deck.Faction == Faction.Neutral)
        {
            errors.Add("A deck must belong to a faction, not Neutral.");
        }

        if (!leaders.TryGetValue(deck.LeaderId, out var leader))
        {
            errors.Add($"Unknown leader '{deck.LeaderId}'.");
        }
        else if (leader.Faction != deck.Faction)
        {
            errors.Add($"Leader '{leader.Id}' belongs to {FactionInfo.Name(leader.Faction)}, not {FactionInfo.Name(deck.Faction)}.");
        }

        var units = 0;
        var specials = 0;
        foreach (var entry in deck.Cards)
        {
            if (!cards.TryGetValue(entry.CardId, out var card))
            {
                errors.Add($"Unknown card '{entry.CardId}'.");
                continue;
            }

            if (entry.Count < 1)
            {
                errors.Add($"Card '{card.Id}' must have a count of at least 1.");
            }

            if (card.Faction != Faction.Neutral && card.Faction != deck.Faction)
            {
                errors.Add($"Card '{card.Id}' belongs to {FactionInfo.Name(card.Faction)} and cannot be used in a {FactionInfo.Name(deck.Faction)} deck.");
            }

            var maxCopies = card.IsHero ? rules.MaxHeroCopies : rules.MaxCopiesPerCard;
            if (entry.Count > maxCopies)
            {
                errors.Add($"Card '{card.Id}' has {entry.Count} copies; the maximum is {maxCopies}.");
            }

            if (card.IsUnit)
            {
                units += entry.Count;
            }
            else
            {
                specials += entry.Count;
            }
        }

        if (units < rules.MinUnits)
        {
            errors.Add($"The deck has {units} unit cards; at least {rules.MinUnits} are required.");
        }

        if (specials > rules.MaxSpecials)
        {
            errors.Add($"The deck has {specials} special cards; at most {rules.MaxSpecials} are allowed.");
        }

        return errors;
    }
}
