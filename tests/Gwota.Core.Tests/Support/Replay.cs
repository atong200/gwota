using Gwota.Core.Ai;

namespace Gwota.Core.Tests.Support;

/// <summary>Picks a uniformly random legal command, so replays reach paths the heuristic AI avoids (redraws, odd targets).</summary>
internal sealed class RandomController(ulong seed) : IPlayerController
{
    private readonly SeededRandom _random = new(seed);

    public GameCommand Decide(MatchEngine engine, Seat seat) => _random.Pick(engine.GetLegalCommands(seat));
}

/// <summary>Plays seeded matches and folds every command, event and intermediate state into one stable hash.</summary>
internal static class Replay
{
    private static readonly LeaderAbility[] Abilities = Enum.GetValues<LeaderAbility>();
    private static readonly Faction[] Factions = [Faction.Realms, Faction.Abyss, Faction.Horde];

    /// <summary>
    /// A varied but fixed match for <paramref name="seed"/>: factions, leader abilities and controllers
    /// (heuristic or random) all rotate with the seed.
    /// </summary>
    public static (MatchEngine Engine, IPlayerController One, IPlayerController Two) Setup(ulong seed)
    {
        var index = (int)seed;
        var one = WithLeader(TestDecks.Mixed(Factions[index % 3]), Abilities[index % Abilities.Length], RowType.Siege);
        var two = WithLeader(TestDecks.Mixed(Factions[(index + 1) % 3]), Abilities[(index * 3) % Abilities.Length], RowType.Ranged);
        var engine = MatchEngine.Create(one, two, new SeededRandom(seed));

        IPlayerController Controller(int slot) => (index + slot) % 3 == 0 ? new RandomController(seed * 31 + (ulong)slot) : new HeuristicAi();
        return (engine, Controller(0), Controller(1));
    }

    /// <summary>Plays the match of <paramref name="seed"/> to the end and returns its fingerprint.</summary>
    public static ulong Fingerprint(ulong seed, Action<MatchEngine, GameCommand>? beforeApply = null)
    {
        var (engine, one, two) = Setup(seed);
        var hash = new Fnv64();
        AddEvents(ref hash, engine.InitialEvents);

        for (var step = 0; !engine.IsFinished; step++)
        {
            step.ShouldBeLessThan(1000, $"seed {seed}: match did not finish");
            var seat = engine.State.SeatToAct!.Value;
            var command = (seat == Seat.One ? one : two).Decide(engine, seat);
            beforeApply?.Invoke(engine, command);

            var result = engine.Apply(command);
            result.IsSuccess.ShouldBeTrue($"seed {seed}: {result.Error}");
            hash.Add(command.ToString());
            AddEvents(ref hash, result.Events);
            AddChecksum(ref hash, StateChecksum.Compute(engine.State));
        }

        return hash.Value;
    }

    private static PlayerSetup WithLeader(PlayerSetup setup, LeaderAbility ability, RowType row) =>
        setup with { Leader = TestCards.Leader(ability, row, setup.Faction) };

    private static void AddChecksum(ref Fnv64 hash, ulong checksum)
    {
        hash.Add((int)checksum);
        hash.Add((int)(checksum >> 32));
    }

    private static void AddEvents(ref Fnv64 hash, IReadOnlyList<GameEvent> events)
    {
        hash.Add(events.Count);
        foreach (var gameEvent in events)
        {
            hash.Add(Describe(gameEvent));
        }
    }

    /// <summary>A stable text for an event: its type plus the ids and values it carries, independent of collection types.</summary>
    private static string Describe(GameEvent gameEvent)
    {
        static string Ids(IEnumerable<int> ids) => string.Join(',', ids);
        static string Cards(IEnumerable<CardInstance> cards) => Ids(cards.Select(c => c.Id));

        var details = gameEvent switch
        {
            MatchStarted e => $"{e.FirstPlayer}",
            CardsDrawn e => $"{e.Seat}:{Ids(e.CardIds)}",
            DraftFinished e => $"{e.Seat}:{Ids(e.ReturnedCardIds)}:{Ids(e.DrawnCardIds)}",
            RoundStarted e => $"{e.Round}:{e.Starter}",
            TurnStarted e => $"{e.Seat}",
            CardPlayed e => $"{e.Player}:{e.Card.Id}:{e.Side}:{e.Row}",
            CardsMustered e => $"{e.Player}:{Cards(e.Cards)}",
            CardRevived e => $"{e.Player}:{e.Card.Id}",
            CardsDestroyed e => Cards(e.Cards),
            CardReturnedToHand e => $"{e.Player}:{e.Card.Id}",
            WeatherCleared => string.Empty,
            LeaderUsed e => $"{e.Player}:{e.Leader.Id}",
            ChoiceRequested e => $"{e.Choice.Seat}:{e.Choice.Kind}:{Ids(e.Choice.Options)}",
            PlayerPassed e => $"{e.Seat}:{e.Automatic}",
            RoundEnded e => $"{e.Result.Round}:{e.Result.ScoreOne}:{e.Result.ScoreTwo}:{e.Result.Winner}",
            PerkTriggered e => $"{e.Seat}:{e.Faction}:{e.Description}",
            PlayerForfeited e => $"{e.Seat}",
            MatchEnded e => $"{e.Outcome.Winner}:{e.Outcome.ForfeitedBy}",
            _ => throw new InvalidOperationException($"Add {gameEvent.GetType().Name} to the replay fingerprint."),
        };

        return $"{gameEvent.GetType().Name}({details})";
    }
}
