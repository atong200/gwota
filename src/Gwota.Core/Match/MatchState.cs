namespace Gwota.Core.Match;

public enum MatchPhase
{
    /// <summary>Before round 1: each player may redraw a few cards of the starting hand.</summary>
    Draft,
    Playing,
    Finished,
}

public enum ChoiceKind
{
    /// <summary>A medic was played: pick a unit from the discard pile to revive.</summary>
    MedicRevive,

    /// <summary>A leader ability: pick a card from the discard pile to return to hand.</summary>
    ReturnFromDiscard,
}

/// <summary>A decision the given player must make before the match can continue.</summary>
public sealed record PendingChoice(Seat Seat, ChoiceKind Kind, IReadOnlyList<int> Options);

/// <summary>Scores at the end of a round. <see cref="Winner"/> is null on a tie.</summary>
public sealed record RoundResult(int Round, int ScoreOne, int ScoreTwo, Seat? Winner)
{
    public int ScoreOf(Seat seat) => seat == Seat.One ? ScoreOne : ScoreTwo;
}

/// <summary>Final result. <see cref="Winner"/> is null on a draw.</summary>
public sealed record MatchOutcome(Seat? Winner, Seat? ForfeitedBy = null)
{
    public bool IsDraw => Winner is null;
}

/// <summary>The full state of a match. Mutated only by the engine; read freely by UI and AI.</summary>
public sealed class MatchState
{
    private readonly PlayerState[] _players;

    internal MatchState(PlayerState one, PlayerState two) => _players = [one, two];

    public IReadOnlyList<PlayerState> Players => _players;

    public MatchPhase Phase { get; internal set; } = MatchPhase.Draft;

    /// <summary>1-based round number.</summary>
    public int Round { get; internal set; } = 1;

    /// <summary>Whose turn it is (meaningful during <see cref="MatchPhase.Playing"/>).</summary>
    public Seat ActiveSeat { get; internal set; }

    /// <summary>Who started the current round.</summary>
    public Seat RoundStarter { get; internal set; }

    public PendingChoice? PendingChoice { get; internal set; }

    /// <summary>Weather cards currently in play; they affect both sides.</summary>
    public IReadOnlyList<CardInstance> Weather => WeatherList;

    public IReadOnlyList<RoundResult> RoundResults => RoundResultList;

    public MatchOutcome? Outcome { get; internal set; }

    internal List<CardInstance> WeatherList { get; } = [];

    internal List<RoundResult> RoundResultList { get; } = [];

    internal int NextInstanceId { get; set; } = 1;

    public PlayerState Player(Seat seat) => _players[seat.Index()];

    /// <summary>
    /// The seat expected to send the next command: the owner of a pending choice, the first player still
    /// drafting (both may draft in any order), or the active seat. Null once the match is over.
    /// </summary>
    public Seat? SeatToAct => Phase switch
    {
        MatchPhase.Finished => null,
        MatchPhase.Draft => _players.FirstOrDefault(p => !p.HasFinishedDraft)?.Seat,
        _ => PendingChoice?.Seat ?? ActiveSeat,
    };

    public bool IsWeatherActive(RowType row) => WeatherList.Any(w => w.Definition.Rows.Contains(row));

    /// <summary>Finds a card anywhere in the match (hands, decks, discards, board, weather).</summary>
    public CardInstance? FindCard(int cardId) =>
        _players
            .SelectMany(p => p.DeckList.Concat(p.HandList).Concat(p.DiscardList).Concat(p.BoardCards))
            .Concat(WeatherList)
            .FirstOrDefault(c => c.Id == cardId);

    internal MatchState Clone()
    {
        var copy = new MatchState(_players[0].Clone(), _players[1].Clone())
        {
            Phase = Phase,
            Round = Round,
            ActiveSeat = ActiveSeat,
            RoundStarter = RoundStarter,
            PendingChoice = PendingChoice,
            Outcome = Outcome,
            NextInstanceId = NextInstanceId,
        };
        copy.WeatherList.AddRange(WeatherList);
        copy.RoundResultList.AddRange(RoundResultList);
        return copy;
    }
}
