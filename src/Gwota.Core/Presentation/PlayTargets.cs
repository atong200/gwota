namespace Gwota.Core.Presentation;

/// <summary>
/// What a selected hand card or the selected leader can act on, derived from <see cref="LegalCommands"/>: played on
/// <see cref="Rows"/> (units, horns), swapped with one of <see cref="Cards"/> (decoys), or <see cref="IsBoardWide"/>
/// (no target, click anywhere).
/// </summary>
public sealed class PlayTargets
{
    private readonly Dictionary<(Seat Side, RowType Row), GameCommand> _rows;
    private readonly Dictionary<int, GameCommand> _cards;
    private readonly GameCommand? _boardWide;
    private readonly HashSet<(Seat Side, RowType Row)> _affectedRows;

    private PlayTargets(
        Dictionary<(Seat, RowType), GameCommand> rows,
        Dictionary<int, GameCommand> cards,
        GameCommand? boardWide,
        HashSet<(Seat, RowType)> affectedRows,
        bool affectsWeatherLane,
        bool affectsOwnDiscard = false)
    {
        _rows = rows;
        _cards = cards;
        _boardWide = boardWide;
        _affectedRows = affectedRows;
        AffectsWeatherLane = affectsWeatherLane;
        AffectsOwnDiscard = affectsOwnDiscard;
    }

    public static PlayTargets None { get; } = new([], [], null, [], affectsWeatherLane: false);

    /// <summary>Row slots (side of the board, row) that accept the card.</summary>
    public IReadOnlyCollection<(Seat Side, RowType Row)> Rows => _rows.Keys;

    /// <summary>Board cards the card can target (decoy swaps).</summary>
    public IReadOnlyCollection<int> Cards => _cards.Keys;

    /// <summary>True when no target is needed: any click on the board plays the card or uses the leader.</summary>
    public bool IsBoardWide => _boardWide is not null;

    /// <summary>Rows a board-wide play acts on (scorch: every row; weather: its rows on both sides; leader: its row).</summary>
    public IReadOnlyCollection<(Seat Side, RowType Row)> AffectedRows => _affectedRows;

    /// <summary>True when a board-wide play acts on the weather lane (weather, clear weather and the matching leaders).</summary>
    public bool AffectsWeatherLane { get; }

    /// <summary>True when the play takes a card from the player's own discard pile (the return-from-discard leader).</summary>
    public bool AffectsOwnDiscard { get; }

    /// <summary>True when nothing can be played right now.</summary>
    public bool IsEmpty => _rows.Count == 0 && _cards.Count == 0 && _boardWide is null;

    /// <summary>Computes the targets of <paramref name="cardId"/> (a card in <paramref name="seat"/>'s hand) from the legal commands.</summary>
    public static PlayTargets For(MatchEngine engine, Seat seat, int cardId)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return For(engine.State, engine.GetLegalCommands(seat), seat, cardId);
    }

    /// <summary>Same as <see cref="For(MatchEngine, Seat, int)"/>, from <paramref name="seat"/>'s already computed legal commands.</summary>
    public static PlayTargets For(MatchState state, IReadOnlyList<GameCommand> legal, Seat seat, int cardId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(legal);

        var options = legal.OfType<PlayCardCommand>().Where(c => c.CardId == cardId).ToList();
        if (options.Count == 0 || state.Player(seat).FindInHand(cardId) is not { } card)
        {
            return None;
        }

        var side = card.Definition.PlacementSide(seat);
        var rows = new Dictionary<(Seat, RowType), GameCommand>();
        var cards = new Dictionary<int, GameCommand>();
        GameCommand? boardWide = null;

        foreach (var option in options)
        {
            if (option.TargetId is { } target)
            {
                cards.TryAdd(target, option);
            }
            else if (option.Row is { } row)
            {
                rows.TryAdd((side, row), option);
            }
            else
            {
                boardWide ??= option;
            }
        }

        return boardWide is null
            ? new PlayTargets(rows, cards, null, [], affectsWeatherLane: false)
            : new PlayTargets(rows, cards, boardWide, AffectedBy(card.Definition), card.Definition.Is(SpecialEffect.Weather) || card.Definition.Is(SpecialEffect.ClearWeather));
    }

    /// <summary>
    /// The leader of <paramref name="seat"/> as a play, with what its ability acts on highlighted: a board-wide use
    /// (any board click uses it), or the units a targeted ability can pick. <see cref="None"/> when the leader cannot
    /// be used right now.
    /// </summary>
    public static PlayTargets ForLeader(MatchEngine engine, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return ForLeader(engine.State, engine.GetLegalCommands(seat), seat);
    }

    /// <summary>Same as <see cref="ForLeader(MatchEngine, Seat)"/>, from <paramref name="seat"/>'s already computed legal commands.</summary>
    public static PlayTargets ForLeader(MatchState state, IReadOnlyList<GameCommand> legal, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(legal);

        var uses = legal.OfType<UseLeaderCommand>().ToList();
        if (uses.Count == 0)
        {
            return None;
        }

        var targeted = new Dictionary<int, GameCommand>();
        foreach (var candidate in uses)
        {
            if (candidate.TargetId is { } target)
            {
                targeted.TryAdd(target, candidate);
            }
        }

        if (targeted.Count > 0)
        {
            return new PlayTargets([], targeted, null, [], affectsWeatherLane: false);
        }

        var use = uses[0];
        var leader = state.Player(seat).Leader;
        HashSet<(Seat, RowType)> rows = (leader.Ability, leader.Row) switch
        {
            (LeaderAbility.PlayWeatherFromDeck, { } row) => [(seat, row), (seat.Opponent(), row)],
            (LeaderAbility.HornRow, { } row) => [(seat, row)],
            (LeaderAbility.ScorchRow, { } row) => [(seat.Opponent(), row)],
            (LeaderAbility.Scorch, _) => BothSides(Enum.GetValues<RowType>()),
            _ => [],
        };
        var weatherLane = leader.Ability is LeaderAbility.ClearWeather or LeaderAbility.PlayWeatherFromDeck;
        return new PlayTargets([], [], use, rows, weatherLane, leader.Ability == LeaderAbility.ReturnFromDiscard);
    }

    private static HashSet<(Seat, RowType)> AffectedBy(CardDefinition card)
    {
        IEnumerable<RowType> rows = card.Effect switch
        {
            SpecialEffect.Scorch => Enum.GetValues<RowType>(),
            SpecialEffect.Weather => card.Rows,
            _ => [],
        };
        return BothSides(rows);
    }

    private static HashSet<(Seat, RowType)> BothSides(IEnumerable<RowType> rows) =>
        [.. rows.SelectMany(row => new[] { (Seat.One, row), (Seat.Two, row) })];

    public bool AcceptsRow(Seat side, RowType row) => _rows.ContainsKey((side, row));

    /// <summary>True when the row should be highlighted: a target row, or a row the board-wide play acts on.</summary>
    public bool Highlights(Seat side, RowType row) => _rows.ContainsKey((side, row)) || _affectedRows.Contains((side, row));

    public bool AcceptsCard(int cardId) => _cards.ContainsKey(cardId);

    /// <summary>Command for a click on a row; falls back to the board-wide play. Null when the click does nothing.</summary>
    public GameCommand? ForRow(Seat side, RowType row) => _rows.GetValueOrDefault((side, row)) ?? _boardWide;

    /// <summary>Command for a click on a board card; falls back to the board-wide play. Null when the click does nothing.</summary>
    public GameCommand? ForCard(int cardId) => _cards.GetValueOrDefault(cardId) ?? _boardWide;

    /// <summary>Command for a click elsewhere on the board (weather lane, empty space). Only board-wide plays react.</summary>
    public GameCommand? ForBoard() => _boardWide;
}
