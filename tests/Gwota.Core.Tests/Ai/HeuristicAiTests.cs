using Gwota.Core.Ai;

namespace Gwota.Core.Tests.Ai;

public class HeuristicAiTests
{
    private readonly HeuristicAi _ai = new();

    [Fact]
    public void Keeps_its_hand_in_the_draft_phase()
    {
        var engine = MatchRunner.NewMatch(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Hells), seed: 1);

        _ai.Decide(engine, Seat.One).ShouldBe(FinishDraftCommand.KeepHand(Seat.One));
    }

    [Fact]
    public void Throws_when_asked_to_act_out_of_turn()
    {
        var engine = new Scenario().Build();

        Should.Throw<InvalidOperationException>(() => _ai.Decide(engine, Seat.Two));
    }

    [Fact]
    public void Plays_the_strongest_card_when_behind()
    {
        var scenario = new Scenario();
        scenario.AddToHand(Seat.One, TestCards.Unit(2));
        var strong = scenario.AddToHand(Seat.One, TestCards.Unit(8));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        scenario.One.Gems = 1;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PlayCardCommand(Seat.One, strong.Id, RowType.Melee));
    }

    [Fact]
    public void Does_not_waste_a_decoy_on_a_plain_unit_when_a_real_play_exists()
    {
        var scenario = new Scenario(withFillers: false);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(2, id: "creep"));
        scenario.AddToHand(Seat.One, TestCards.Decoy());
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(2, RowType.Ranged));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(10));
        scenario.One.Gems = 1;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PlayCardCommand(Seat.One, unit.Id, RowType.Ranged));
    }

    [Fact]
    public void Prefers_a_spy_for_card_advantage()
    {
        var scenario = new Scenario();
        scenario.AddToHand(Seat.One, TestCards.Unit(3));
        var spy = scenario.AddToHand(Seat.One, TestCards.Spy());
        scenario.AddToDeck(Seat.One, TestCards.Unit(4));
        scenario.AddToDeck(Seat.One, TestCards.Unit(4));
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PlayCardCommand(Seat.One, spy.Id, RowType.Melee));
    }

    [Fact]
    public void Passes_when_ahead_of_a_passed_opponent()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        scenario.Two.HasPassed = true;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PassCommand(Seat.One));
    }

    [Fact]
    public void Takes_the_lead_against_a_passed_opponent_with_the_cheapest_winning_move()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        scenario.AddToHand(Seat.One, TestCards.Unit(9));
        var enough = scenario.AddToHand(Seat.One, TestCards.Unit(6, RowType.Ranged));
        scenario.Two.HasPassed = true;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PlayCardCommand(Seat.One, enough.Id, RowType.Ranged));
    }

    [Fact]
    public void Gives_up_a_hopeless_round_when_it_still_has_a_gem_to_spare()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(30));
        scenario.AddToHand(Seat.One, TestCards.Unit(5));
        scenario.Two.HasPassed = true;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PassCommand(Seat.One));
    }

    [Fact]
    public void Fights_a_hopeless_round_when_it_is_the_last_gem()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(30));
        scenario.AddToHand(Seat.One, TestCards.Unit(5));
        scenario.Two.HasPassed = true;
        scenario.One.Gems = 1;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBeOfType<PlayCardCommand>();
    }

    [Fact]
    public void Passes_with_a_comfortable_lead_to_save_cards()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(20));
        scenario.AddToHand(Seat.One, TestCards.Unit(5));
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PassCommand(Seat.One));
    }

    [Fact]
    public void Passes_when_ahead_without_a_hand_disadvantage_to_make_the_opponent_spend_cards()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(4));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(3));
        AddUnits(scenario, Seat.One, 6);
        AddUnits(scenario, Seat.Two, 6);
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PassCommand(Seat.One));
    }

    [Fact]
    public void Keeps_playing_when_ahead_but_down_on_cards()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(4));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(3));
        AddUnits(scenario, Seat.One, 6);
        AddUnits(scenario, Seat.Two, 8);
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBeOfType<PlayCardCommand>();
    }

    [Fact]
    public void Chases_the_lead_while_it_has_cards_to_spare()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        AddUnits(scenario, Seat.One, HeuristicAiOptions.Default.ReserveHandSize + 1);
        AddUnits(scenario, Seat.Two, 6);
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBeOfType<PlayCardCommand>();
    }

    [Fact]
    public void Keeps_its_reserve_for_later_rounds_when_behind_with_a_gem_to_spare()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        AddUnits(scenario, Seat.One, HeuristicAiOptions.Default.ReserveHandSize - 1);
        AddUnits(scenario, Seat.Two, 6);
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBe(new PassCommand(Seat.One));
    }

    [Fact]
    public void Spends_its_reserve_when_the_round_decides_the_match()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        AddUnits(scenario, Seat.One, HeuristicAiOptions.Default.ReserveHandSize);
        scenario.One.Gems = 1;
        var engine = scenario.Build();

        _ai.Decide(engine, Seat.One).ShouldBeOfType<PlayCardCommand>();
    }

    [Fact]
    public void Ai_vs_ai_matches_spread_cards_across_rounds()
    {
        var firstRoundPlays = 0;
        var matches = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var one = TestDecks.Mixed(Faction.Realms);
            var two = TestDecks.Mixed(Faction.Hells);
            var result = MatchRunner.Run(one, two, seed);
            firstRoundPlays += CountFirstRoundCardPlays(MatchRunner.NewMatch(one, two, seed), result.Commands);
            matches++;
        }

        // Each side starts with 10 cards; dumping the whole hand in round 1 would average 20 plays.
        ((double)firstRoundPlays / matches).ShouldBeLessThan(14);
    }

    private static void AddUnits(Scenario scenario, Seat seat, int count)
    {
        for (var i = 0; i < count; i++)
        {
            scenario.AddToHand(seat, TestCards.Unit(3, id: $"unit_{seat}_{i}"));
        }
    }

    private static int CountFirstRoundCardPlays(MatchEngine replay, IReadOnlyList<GameCommand> commands)
    {
        var plays = 0;
        foreach (var command in commands)
        {
            if (replay.State.Round > 1)
            {
                break;
            }

            if (command is PlayCardCommand)
            {
                plays++;
            }

            replay.ApplyOk(command);
        }

        return plays;
    }

    [Fact]
    public void Resolves_its_own_choices_with_the_best_option()
    {
        var scenario = new Scenario();
        var medic = scenario.AddToHand(Seat.One, TestCards.Medic());
        scenario.AddToDiscard(Seat.One, TestCards.Unit(2));
        var best = scenario.AddToDiscard(Seat.One, TestCards.Unit(7));
        var engine = scenario.Build();
        engine.Play(medic);

        _ai.Decide(engine, Seat.One).ShouldBe(new ResolveChoiceCommand(Seat.One, best.Id));
    }

    [Fact]
    public void Decisions_are_deterministic()
    {
        var a = MatchRunner.Run(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Hells), seed: 42);
        var b = MatchRunner.Run(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Hells), seed: 42);

        a.Commands.ShouldBe(b.Commands);
        a.Outcome.ShouldBe(b.Outcome);
    }

    [Fact]
    public void Ai_vs_ai_matches_always_finish_with_legal_moves()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = MatchRunner.Run(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Hells), seed);

            result.Engine.IsFinished.ShouldBeTrue($"seed {seed}");
            result.Engine.State.RoundResults.Count.ShouldBeInRange(2, 3, $"seed {seed}");
        }
    }
}
