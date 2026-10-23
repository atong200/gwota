namespace Gwota.Core.Presentation;

/// <summary>Player-facing rules text for cards and leaders (tooltips, card details).</summary>
public static class CardText
{
    public static string Ability(UnitAbility ability) => AbilityLine(ability).Plain;

    public static string Effect(SpecialEffect effect, IReadOnlyList<RowType> rows) => EffectLine(effect, rows).Plain;

    public static CardTextLine AbilityLine(UnitAbility ability) => ability switch
    {
        UnitAbility.Spy => new("placed on your opponent's side; you draw 2 cards.", "Spy"),
        UnitAbility.Medic => new("revive a non-hero unit from your discard pile.", "Medic"),
        UnitAbility.Muster => new("also plays every card of its group from your deck.", "Muster"),
        UnitAbility.TightBond => new("strength multiplied by copies of this card in the row.", "Tight Bond"),
        UnitAbility.MoraleBoost => new("+1 to every other unit in its row.", "Morale Boost"),
        UnitAbility.Horn => new("doubles every other unit in its row.", "Horn"),
        UnitAbility.ScorchRow => new("destroys the strongest enemy unit(s) in this row if the row totals 10+.", "Scorch"),
        _ => throw new ArgumentOutOfRangeException(nameof(ability), ability, null),
    };

    public static CardTextLine EffectLine(SpecialEffect effect, IReadOnlyList<RowType> rows) => effect switch
    {
        SpecialEffect.Horn => new("doubles the non-hero units of one of your rows.", "Horn"),
        SpecialEffect.Scorch => new("destroys the strongest non-hero unit(s) on the battlefield.", "Scorch"),
        SpecialEffect.Decoy => new("swap with one of your units to return it to your hand.", "Decoy"),
        SpecialEffect.Weather => new($"non-hero {RowList(rows)} units on both sides drop to 1.", "Weather"),
        SpecialEffect.ClearWeather => new("removes all weather effects.", "Clear Weather"),
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
    };

    /// <summary>The deck builder's copy counter, e.g. "x3" (copies left to add, or the most a deck may hold).</summary>
    public static string Copies(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return $"x{count}";
    }

    /// <summary>Hover text for a pile, e.g. "12 cards in draw pile", "1 card in discard pile", "No cards in draw pile".</summary>
    public static string PileCount(int count, string pileName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentException.ThrowIfNullOrWhiteSpace(pileName);

        var cards = count switch
        {
            0 => "No cards",
            1 => "1 card",
            _ => $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} cards",
        };
        return $"{cards} in {pileName}";
    }

    /// <summary>One-line deck summary for the deck list, e.g. "22 units, 4 specials, 1 hero - 98 strength".</summary>
    public static string DeckSummary(DeckBuilding.DeckStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var parts = new List<string> { Counted(stats.Units, "unit"), Counted(stats.Specials, "special") };
        if (stats.Heroes > 0)
        {
            parts.Add(Counted(stats.Heroes, "hero"));
        }

        return $"{string.Join(", ", parts)} - {stats.TotalStrength.ToString(System.Globalization.CultureInfo.InvariantCulture)} strength";
    }

    private static string Counted(int count, string noun) =>
        $"{count.ToString(System.Globalization.CultureInfo.InvariantCulture)} {noun}{(count == 1 ? string.Empty : noun.EndsWith('o') ? "es" : "s")}";

    public static string Leader(LeaderDefinition leader)
    {
        ArgumentNullException.ThrowIfNull(leader);
        var row = leader.Row is { } r ? RowList([r]) : "?";
        return leader.Ability switch
        {
            LeaderAbility.ClearWeather => "Clear all weather effects.",
            LeaderAbility.PlayWeatherFromDeck => $"Play a {row} weather card from your deck.",
            LeaderAbility.HornRow => $"Double your {row} row.",
            LeaderAbility.ScorchRow => $"Destroy the strongest enemy {row} unit(s) if that row totals 10+.",
            LeaderAbility.ReturnFromDiscard => "Return a card from your discard pile to your hand.",
            _ => throw new ArgumentOutOfRangeException(nameof(leader), leader.Ability, null),
        };
    }

    /// <summary>Plain multi-line description (name, type line, rules, flavor) for logs and plain tooltips.</summary>
    public static string Describe(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return string.Join('\n', Rules(card).Select(line => line.Plain).Prepend(TypeLine(card)).Prepend(card.Name));
    }

    /// <summary>
    /// The rules of a card as keyword/text lines, then its flavor text, for rich views (the card preview) that show
    /// the name, faction, rows and strength graphically instead of as a type line.
    /// </summary>
    public static IReadOnlyList<CardTextLine> Rules(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var lines = new List<CardTextLine>();
        if (card.IsUnit)
        {
            if (card.IsHero)
            {
                lines.Add(new CardTextLine("immune to weather, horns, scorch and medics. Only one copy per deck.", "Hero"));
            }

            lines.AddRange(card.Abilities.Select(AbilityLine));
        }
        else if (card.Effect is { } effect)
        {
            lines.Add(EffectLine(effect, card.Rows));
        }

        if (!string.IsNullOrWhiteSpace(card.Description))
        {
            lines.Add(new CardTextLine(card.Description, IsFlavor: true));
        }

        return lines;
    }

    public static string TypeLine(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.IsUnit
            ? $"{FactionInfo.Name(card.Faction)} {(card.IsHero ? "hero" : "unit")} - {RowList(card.Rows)} - {card.Strength}"
            : $"{FactionInfo.Name(card.Faction)} special";
    }

    private static string RowList(IReadOnlyList<RowType> rows) =>
        string.Join("/", rows.Select(r => r.ToString().ToLowerInvariant()));
}

/// <summary>One line of card rules text. <see cref="Keyword"/> (e.g. "Spy") is shown emphasized before the text.</summary>
public sealed record CardTextLine(string Text, string? Keyword = null, bool IsFlavor = false)
{
    public string Plain => Keyword is null ? Text : $"{Keyword}: {Text}";
}
