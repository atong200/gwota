namespace Gwota.Core.Ai;

/// <summary>Tuning knobs for <see cref="HeuristicAi"/>.</summary>
public sealed record HeuristicAiOptions
{
    public static HeuristicAiOptions Default { get; } = new();

    /// <summary>
    /// How many strength points one card in hand is worth when comparing moves. A plain unit is worth at most its
    /// strength (so retrieving a weak unit with a decoy is not mistaken for a gain); other cards are worth this value.
    /// </summary>
    public double CardValue { get; init; } = 5;

    /// <summary>
    /// When losing the round is affordable, the AI stops chasing a lead once its hand is down to this many cards,
    /// keeping them for the following rounds.
    /// </summary>
    public int ReserveHandSize { get; init; } = 5;
}

/// <summary>
/// Deterministic one-ply AI: scores each move on a cloned engine by board difference plus hand value.
/// Passing uses round-economy heuristics; the opponent's hand counts only by its size (no hidden information).
/// </summary>
public sealed class HeuristicAi(HeuristicAiOptions? options = null) : IPlayerController
{
    private readonly HeuristicAiOptions _options = options ?? HeuristicAiOptions.Default;

    public GameCommand Decide(MatchEngine engine, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var legal = engine.GetLegalCommands(seat);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException($"Seat {seat} has no legal command to decide on.");
        }

        if (engine.State.Phase == MatchPhase.Draft)
        {
            return FinishDraftCommand.KeepHand(seat);
        }

        if (engine.State.PendingChoice is not null)
        {
            return BestByEvaluation(engine, seat, legal).Command;
        }

        var pass = legal.OfType<PassCommand>().FirstOrDefault();
        var plays = legal.Where(c => c is not PassCommand).ToList();
        if (pass is null)
        {
            return BestByEvaluation(engine, seat, plays).Command;
        }

        if (plays.Count == 0)
        {
            return pass;
        }

        return engine.State.Player(seat.Opponent()).HasPassed
            ? DecideAgainstPassedOpponent(engine, seat, plays, pass)
            : DecideWhileOpponentPlays(engine, seat, plays, pass);
    }

    private GameCommand DecideAgainstPassedOpponent(MatchEngine engine, Seat seat, List<GameCommand> plays, PassCommand pass)
    {
        if (Lead(engine, seat) > 0)
        {
            return pass;
        }

        // Cheapest way to take the lead: keep the most resources (cards, leader), then win by the smallest margin
        // so stronger cards are saved for later rounds.
        var winning = plays
            .Select(command => Simulate(engine, seat, command))
            .Where(s => s.Lead > 0)
            .OrderByDescending(s => s.Evaluation - s.Lead)
            .ThenBy(s => s.Lead)
            .FirstOrDefault();
        if (winning is not null)
        {
            return winning.Command;
        }

        // Cannot win with one more card: keep fighting only if losing this round loses the match.
        var losingRoundLosesMatch = engine.State.Player(seat).Gems <= 1;
        return losingRoundLosesMatch ? BestByEvaluation(engine, seat, plays).Command : pass;
    }

    private GameCommand DecideWhileOpponentPlays(MatchEngine engine, Seat seat, List<GameCommand> plays, PassCommand pass)
    {
        var me = engine.State.Player(seat);
        var opponent = engine.State.Player(seat.Opponent());
        var lead = Lead(engine, seat);
        var canAffordToLoseRound = me.Gems > 1;

        if (canAffordToLoseRound)
        {
            // Ahead without a hand disadvantage: make the opponent spend cards to win the round.
            if (lead > 0 && me.Hand.Count >= opponent.Hand.Count - 1)
            {
                return pass;
            }

            // Behind with a small hand: keep the remaining cards for the next rounds.
            if (lead < 0 && me.Hand.Count <= _options.ReserveHandSize)
            {
                return pass;
            }
        }

        return BestByEvaluation(engine, seat, plays).Command;
    }

    private Simulation BestByEvaluation(MatchEngine engine, Seat seat, IEnumerable<GameCommand> commands) =>
        commands
            .Select(command => Simulate(engine, seat, command))
            .OrderByDescending(s => s.Evaluation)
            .First();

    private Simulation Simulate(MatchEngine engine, Seat seat, GameCommand command)
    {
        var copy = engine.Clone();
        var round = copy.State.Round;
        copy.Apply(command);

        // Follow up our own pending choices greedily (e.g. medic chains) so the move is valued fully.
        while (copy.State.PendingChoice is { } choice && choice.Seat == seat && copy.State.Round == round)
        {
            var best = choice.Options
                .Select(id => new ResolveChoiceCommand(seat, id))
                .Select(resolve => (Command: resolve, Value: Evaluate(Apply(copy.Clone(), resolve), seat)))
                .OrderByDescending(x => x.Value)
                .First();
            copy.Apply(best.Command);
        }

        return new Simulation(command, Evaluate(copy, seat), Lead(copy, seat));
    }

    private static MatchEngine Apply(MatchEngine engine, GameCommand command)
    {
        engine.Apply(command);
        return engine;
    }

    private double Evaluate(MatchEngine engine, Seat seat)
    {
        var me = engine.State.Player(seat);
        var opponent = engine.State.Player(seat.Opponent());
        var handValue = me.Hand.Sum(card => HandValue(card.Definition)) - (opponent.Hand.Count * _options.CardValue);
        var leaderBonus = me.LeaderUsed ? 0 : _options.CardValue / 2;
        return Lead(engine, seat) + handValue + leaderBonus;
    }

    private double HandValue(CardDefinition card) =>
        card.IsUnit && card.Abilities.Count == 0
            ? Math.Min(card.Strength, _options.CardValue)
            : _options.CardValue;

    private static int Lead(MatchEngine engine, Seat seat) => engine.Score(seat) - engine.Score(seat.Opponent());

    private sealed record Simulation(GameCommand Command, double Evaluation, int Lead);
}
