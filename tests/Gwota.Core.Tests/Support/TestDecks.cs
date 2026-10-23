using Gwota.Core.Ai;

namespace Gwota.Core.Tests.Support;

/// <summary>Synthetic decks exercising every ability, for full-match tests independent of shipped content.</summary>
internal static class TestDecks
{
    public static PlayerSetup Mixed(Faction faction)
    {
        var p = faction.ToString().ToLowerInvariant();
        CardDefinition Own(CardDefinition card) => card with { Id = $"{p}_{card.Id}", Faction = faction };

        var bond = Own(TestCards.WithAbility(4, UnitAbility.TightBond));
        var muster = Own(TestCards.Muster(2, $"{p}_pack"));
        var cards = new List<CardDefinition>
        {
            Own(TestCards.Hero(10)),
            Own(TestCards.Hero(8, RowType.Ranged, "hero_ranged")),
            Own(TestCards.Spy(3)),
            Own(TestCards.Spy(2, RowType.Ranged) with { Id = "spy_ranged" }),
            Own(TestCards.Medic(5)),
            Own(TestCards.Medic(4, RowType.Ranged) with { Id = "medic_ranged" }),
            bond, bond, bond,
            muster, muster, muster,
            Own(TestCards.WithAbility(3, UnitAbility.MoraleBoost, RowType.Siege)),
            Own(TestCards.WithAbility(2, UnitAbility.Horn, RowType.Ranged)),
            Own(TestCards.WithAbility(5, UnitAbility.ScorchRow)),
            Own(TestCards.Agile(6)),
            Own(TestCards.Unit(8, RowType.Siege)),
            Own(TestCards.Unit(7, RowType.Ranged)),
            Own(TestCards.Unit(6)),
            Own(TestCards.Unit(5, RowType.Siege)),
            Own(TestCards.Unit(4, RowType.Ranged)),
            Own(TestCards.Unit(3)),
            TestCards.Horn(),
            TestCards.Scorch(),
            TestCards.Decoy(),
            TestCards.Decoy(),
            TestCards.Weather(RowType.Melee),
            TestCards.Weather(RowType.Ranged),
            TestCards.Weather(RowType.Ranged, RowType.Siege),
            TestCards.ClearWeather(),
        };

        var leader = faction == Faction.Realms
            ? TestCards.Leader(LeaderAbility.HornRow, RowType.Siege)
            : TestCards.Leader(LeaderAbility.ReturnFromDiscard, faction: Faction.Hells);
        return new PlayerSetup(faction.ToString(), faction, leader, cards);
    }
}

internal sealed record MatchRun(MatchEngine Engine, IReadOnlyList<GameCommand> Commands, MatchOutcome Outcome);

internal static class MatchRunner
{
    public static MatchEngine NewMatch(PlayerSetup one, PlayerSetup two, ulong seed) =>
        MatchEngine.Create(one, two, new SeededRandom(seed));

    /// <summary>Plays a full AI-vs-AI match, failing the test on any rejected command or runaway loop.</summary>
    public static MatchRun Run(PlayerSetup one, PlayerSetup two, ulong seed, int maxCommands = 500)
    {
        var engine = NewMatch(one, two, seed);
        var players = new IPlayerController[] { new HeuristicAi(), new HeuristicAi() };
        var commands = new List<GameCommand>();

        while (!engine.IsFinished)
        {
            commands.Count.ShouldBeLessThan(maxCommands, $"seed {seed}: match did not finish");
            var seat = engine.State.SeatToAct!.Value;
            var command = players[seat.Index()].Decide(engine, seat);
            var result = engine.Apply(command);
            result.IsSuccess.ShouldBeTrue($"seed {seed}: {result.Error}");
            commands.Add(command);
        }

        return new MatchRun(engine, commands, engine.State.Outcome!);
    }
}
