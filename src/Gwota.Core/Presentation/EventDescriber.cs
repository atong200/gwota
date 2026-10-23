namespace Gwota.Core.Presentation;

/// <summary>
/// Turns <see cref="GameEvent"/>s into short, player-facing log lines. Information hidden from
/// <paramref name="viewer"/> (the opponent's drawn cards) is never revealed.
/// </summary>
public static class EventDescriber
{
    /// <summary>Returns the log line for an event, or null for events not worth logging (e.g. turn changes).</summary>
    public static string? Describe(GameEvent gameEvent, MatchState state, Seat viewer)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(state);

        string Name(Seat seat) => state.Player(seat).Name;

        return gameEvent switch
        {
            MatchStarted e => $"{Name(e.FirstPlayer)} goes first.",
            CardsDrawn e when e.CardIds.Count == 0 => null,
            CardsDrawn e => $"{Name(e.Seat)} draws {Plural(e.CardIds.Count, "card")}" + DrawnNames(e, state, viewer) + ".",
            DraftFinished e => e.ReturnedCardIds.Count == 0
                ? $"{Name(e.Seat)} keeps their starting hand."
                : $"{Name(e.Seat)} redraws {Plural(e.ReturnedCardIds.Count, "card")}" + DraftedNames(e, state, viewer) + ".",
            RoundStarted e => $"Round {e.Round} begins. {Name(e.Starter)} starts.",
            CardPlayed e => DescribePlay(e, Name),
            CardsMustered e => $"{Name(e.Player)} musters {Names(e.Cards)}.",
            CardRevived e => $"{Name(e.Player)} revives {e.Card.Definition.Name}.",
            CardsDestroyed e => $"Destroyed: {Names(e.Cards)}.",
            CardReturnedToHand e => $"{Name(e.Player)} takes {e.Card.Definition.Name} back to hand.",
            WeatherCleared => "The skies clear.",
            LeaderUsed e => $"{Name(e.Player)} uses leader {e.Leader.Name}.",
            PlayerPassed e => e.Automatic ? $"{Name(e.Seat)} has no cards left and passes." : $"{Name(e.Seat)} passes.",
            RoundEnded e => DescribeRound(e.Result, Name),
            PerkTriggered e => $"{Name(e.Seat)}: {e.Description}",
            PlayerForfeited e => $"{Name(e.Seat)} forfeits.",
            MatchEnded { Outcome: { Winner: { } winner, ForfeitedBy: not null } } => $"{Name(winner)} wins the match by forfeit.",
            MatchEnded e => e.Outcome.Winner is { } winner ? $"{Name(winner)} wins the match!" : "The match is a draw.",
            _ => null,
        };
    }

    private static string DescribePlay(CardPlayed e, Func<Seat, string> name)
    {
        var card = e.Card.Definition.Name;
        if (e.Side != e.Player)
        {
            return $"{name(e.Player)} sends {card} to {name(e.Side)}'s {RowName(e.Row)} row.";
        }

        return e.Row is null ? $"{name(e.Player)} plays {card}." : $"{name(e.Player)} plays {card} on the {RowName(e.Row)} row.";
    }

    private static string DescribeRound(RoundResult result, Func<Seat, string> name)
    {
        var score = $"Round {result.Round} ends {result.ScoreOne} - {result.ScoreTwo}";
        return result.Winner is { } winner ? $"{score}: {name(winner)} wins the round." : $"{score}: a draw.";
    }

    private static string DrawnNames(CardsDrawn e, MatchState state, Seat viewer)
    {
        if (e.Seat != viewer)
        {
            return string.Empty;
        }

        return CardNames(e.CardIds, state);
    }

    private static string DraftedNames(DraftFinished e, MatchState state, Seat viewer)
    {
        if (e.Seat != viewer)
        {
            return string.Empty;
        }

        var returned = CardNames(e.ReturnedCardIds, state);
        var drawn = CardNames(e.DrawnCardIds, state);
        return returned.Length == 0 || drawn.Length == 0 ? string.Empty : $"{returned} ->{drawn}";
    }

    private static string CardNames(IEnumerable<int> ids, MatchState state)
    {
        var names = ids.Select(id => state.FindCard(id)?.Definition.Name).OfType<string>().ToList();
        return names.Count == 0 ? string.Empty : $" ({string.Join(", ", names)})";
    }

    private static string Names(IEnumerable<CardInstance> cards) => string.Join(", ", cards.Select(c => c.Definition.Name));

    private static string RowName(RowType? row) => row?.ToString().ToLowerInvariant() ?? "?";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
