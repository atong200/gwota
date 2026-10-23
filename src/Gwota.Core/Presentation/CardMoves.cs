namespace Gwota.Core.Presentation;

public enum ZoneKind
{
    Deck,
    Hand,
    Row,

    /// <summary>The horn slot of a row.</summary>
    Horn,
    Weather,
    Discard,

    /// <summary>
    /// Not a real zone: where a one-shot special (scorch, clear weather) is shown between leaving the hand and
    /// reaching the discard pile.
    /// </summary>
    Reveal,
}

/// <summary>
/// Where a card is. <see cref="Seat"/> is the owner for deck, hand, discard and reveal, the board side for rows and
/// horn slots (a spy sits on its opponent's side) and the player who played it for weather.
/// </summary>
public readonly record struct CardZone(ZoneKind Kind, Seat Seat, RowType? Row = null)
{
    public static CardZone Deck(Seat seat) => new(ZoneKind.Deck, seat);

    public static CardZone Hand(Seat seat) => new(ZoneKind.Hand, seat);

    public static CardZone Discard(Seat seat) => new(ZoneKind.Discard, seat);

    public static CardZone Weather(Seat seat) => new(ZoneKind.Weather, seat);

    public static CardZone Reveal(Seat seat) => new(ZoneKind.Reveal, seat);

    public static CardZone OnRow(Seat side, RowType row) => new(ZoneKind.Row, side, row);

    public static CardZone HornOf(Seat side, RowType row) => new(ZoneKind.Horn, side, row);

    public bool IsOnBoard => Kind is ZoneKind.Row or ZoneKind.Horn or ZoneKind.Weather;
}

public sealed record CardLocation(CardInstance Card, CardZone Zone);

/// <summary>The order in which the moves of one command are shown.</summary>
public enum MoveStage
{
    /// <summary>Cards entering the board (played, mustered, revived) or revealed one-shot specials.</summary>
    EnterPlay,

    /// <summary>Cards leaving the board or the reveal spot for a pile (destroyed, round sweep, resolved specials).</summary>
    ToPile,

    /// <summary>Cards arriving in a hand (draws, decoy returns, leader returns).</summary>
    ToHand,
}

public sealed record CardMove(CardInstance Card, CardZone From, CardZone To, MoveStage Stage);

/// <summary>Every card that changed zone during one command, grouped by <see cref="MoveStage"/>.</summary>
public sealed record MovePlan(IReadOnlyList<CardMove> Moves, bool HoldBeforePiles)
{
    public static MovePlan Empty { get; } = new([], false);

    public bool IsEmpty => Moves.Count == 0;

    public IReadOnlyList<CardMove> InStage(MoveStage stage) => Moves.Where(m => m.Stage == stage).ToList();
}

/// <summary>
/// Turns two snapshots of a match (before and after a command) into the card movements to animate, so the
/// front-end does not need one special case per rule: it only has to fly cards from zone to zone.
/// </summary>
public static class CardMoves
{
    /// <summary>The zone of every card in the match, by card id.</summary>
    public static IReadOnlyDictionary<int, CardLocation> Snapshot(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var locations = new Dictionary<int, CardLocation>();
        void Put(IEnumerable<CardInstance> cards, CardZone zone)
        {
            foreach (var card in cards)
            {
                locations[card.Id] = new CardLocation(card, zone);
            }
        }

        foreach (var player in state.Players)
        {
            Put(player.Deck, CardZone.Deck(player.Seat));
            Put(player.Hand, CardZone.Hand(player.Seat));
            Put(player.Discard, CardZone.Discard(player.Seat));
            foreach (var row in player.Rows)
            {
                Put(row.Cards, CardZone.OnRow(player.Seat, row.Type));
                if (row.HornCard is { } horn)
                {
                    Put([horn], CardZone.HornOf(player.Seat, row.Type));
                }
            }
        }

        foreach (var weather in state.Weather)
        {
            Put([weather], CardZone.Weather(weather.Owner));
        }

        return locations;
    }

    /// <summary>
    /// Diffs the snapshots taken before and after a command. A one-shot special (hand straight to discard) becomes a
    /// reveal then a discard; a round end adds a pause before the piles stage.
    /// </summary>
    public static MovePlan Plan(
        IReadOnlyDictionary<int, CardLocation> before,
        IReadOnlyDictionary<int, CardLocation> after,
        IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(events);

        var moves = new List<CardMove>();
        foreach (var (id, now) in after.OrderBy(pair => pair.Key))
        {
            if (!before.TryGetValue(id, out var was) || was.Zone == now.Zone)
            {
                continue;
            }

            if (was.Zone.Kind == ZoneKind.Hand && now.Zone.Kind == ZoneKind.Discard)
            {
                var reveal = CardZone.Reveal(was.Zone.Seat);
                moves.Add(new CardMove(now.Card, was.Zone, reveal, MoveStage.EnterPlay));
                moves.Add(new CardMove(now.Card, reveal, now.Zone, MoveStage.ToPile));
                continue;
            }

            moves.Add(new CardMove(now.Card, was.Zone, now.Zone, StageOf(now.Zone)));
        }

        var ordered = moves.OrderBy(m => m.Stage).ToList();
        var roundEnded = events.Any(e => e is RoundEnded);
        return ordered.Count == 0 ? MovePlan.Empty : new MovePlan(ordered, roundEnded && ordered.Any(m => m.Stage == MoveStage.ToPile));
    }

    private static MoveStage StageOf(CardZone destination) => destination.Kind switch
    {
        ZoneKind.Hand => MoveStage.ToHand,
        ZoneKind.Deck or ZoneKind.Discard => MoveStage.ToPile,
        _ => MoveStage.EnterPlay,
    };
}
