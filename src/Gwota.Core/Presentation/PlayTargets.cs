namespace Gwota.Core.Presentation;

/// <summary>
/// Where a selected hand card can be played, derived from <see cref="LegalCommands"/>: on <see cref="Rows"/> (units,
/// horns), swapped with one of <see cref="Cards"/> (decoys), or <see cref="IsBoardWide"/> (no target, click anywhere).
/// </summary>
public sealed class PlayTargets
{
    private readonly Dictionary<(Seat Side, RowType Row), PlayCardCommand> _rows;
    private readonly Dictionary<int, PlayCardCommand> _cards;
    private readonly PlayCardCommand? _boardWide;

    private PlayTargets(
        Dictionary<(Seat, RowType), PlayCardCommand> rows,
        Dictionary<int, PlayCardCommand> cards,
        PlayCardCommand? boardWide)
    {
        _rows = rows;
        _cards = cards;
        _boardWide = boardWide;
    }

    public static PlayTargets None { get; } = new([], [], null);

    /// <summary>Row slots (side of the board, row) that accept the card.</summary>
    public IReadOnlyCollection<(Seat Side, RowType Row)> Rows => _rows.Keys;

    /// <summary>Board cards the card can target (decoy swaps).</summary>
    public IReadOnlyCollection<int> Cards => _cards.Keys;

    /// <summary>True when the card needs no target: any click on the board plays it.</summary>
    public bool IsBoardWide => _boardWide is not null;

    /// <summary>True when the card cannot be played at all right now.</summary>
    public bool IsEmpty => _rows.Count == 0 && _cards.Count == 0 && _boardWide is null;

    /// <summary>Computes the targets of <paramref name="cardId"/> (a card in <paramref name="seat"/>'s hand) from the legal commands.</summary>
    public static PlayTargets For(MatchEngine engine, Seat seat, int cardId)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var options = engine.GetLegalCommands(seat).OfType<PlayCardCommand>().Where(c => c.CardId == cardId).ToList();
        if (options.Count == 0 || engine.State.FindCard(cardId) is not { } card)
        {
            return None;
        }

        // Spies are played on the opponent's side; everything else on the player's own board.
        var side = card.Definition.Has(UnitAbility.Spy) ? seat.Opponent() : seat;
        var rows = new Dictionary<(Seat, RowType), PlayCardCommand>();
        var cards = new Dictionary<int, PlayCardCommand>();
        PlayCardCommand? boardWide = null;

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

        return new PlayTargets(rows, cards, boardWide);
    }

    public bool AcceptsRow(Seat side, RowType row) => _rows.ContainsKey((side, row));

    public bool AcceptsCard(int cardId) => _cards.ContainsKey(cardId);

    /// <summary>Command for a click on a row; falls back to the board-wide play. Null when the click does nothing.</summary>
    public PlayCardCommand? ForRow(Seat side, RowType row) => _rows.GetValueOrDefault((side, row)) ?? _boardWide;

    /// <summary>Command for a click on a board card; falls back to the board-wide play. Null when the click does nothing.</summary>
    public PlayCardCommand? ForCard(int cardId) => _cards.GetValueOrDefault(cardId) ?? _boardWide;

    /// <summary>Command for a click elsewhere on the board (weather lane, empty space). Only board-wide cards react.</summary>
    public PlayCardCommand? ForBoard() => _boardWide;
}
