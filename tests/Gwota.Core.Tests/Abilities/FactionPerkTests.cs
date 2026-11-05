namespace Gwota.Core.Tests.Abilities;

public class FactionPerkTests
{
    private static void EndRound(MatchEngine engine)
    {
        engine.Pass(Seat.One);
        engine.Pass(Seat.Two);
    }

    [Fact]
    public void Realms_draws_a_card_after_winning_a_round()
    {
        var scenario = new Scenario(factionOne: Faction.Realms);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var top = scenario.AddToDeck(Seat.One, TestCards.Unit(3));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        scenario.One.Hand.ShouldContain(top);
        events.OfType<PerkTriggered>().ShouldHaveSingleItem().Seat.ShouldBe(Seat.One);
    }

    [Fact]
    public void Realms_does_not_draw_with_a_full_hand()
    {
        var scenario = new Scenario(factionOne: Faction.Realms);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        scenario.FillHand(Seat.One, MatchRules.Classic.MaxHandSize);
        var top = scenario.AddToDeck(Seat.One, TestCards.Unit(3));
        var engine = scenario.Build();

        EndRound(engine);

        scenario.One.Hand.Count.ShouldBe(MatchRules.Classic.MaxHandSize);
        scenario.One.Deck.ShouldBe([top]);
    }

    [Fact]
    public void Realms_does_not_draw_after_losing_or_tying()
    {
        var scenario = new Scenario(factionOne: Faction.Realms);
        scenario.AddToDeck(Seat.One, TestCards.Unit(3));
        var engine = scenario.Build();

        EndRound(engine);

        scenario.One.Hand.Count.ShouldBe(1);
        scenario.One.Deck.Count.ShouldBe(1);
    }

    [Fact]
    public void Abyss_keeps_exactly_one_of_its_units_on_the_board()
    {
        var scenario = new Scenario(factionOne: Faction.Abyss);
        var units = Enumerable.Range(0, 4).Select(i => scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(i + 1))).ToList();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(20));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        var kept = scenario.One.BoardCards.ShouldHaveSingleItem();
        units.ShouldContain(kept);
        scenario.One.Discard.Count.ShouldBe(3);
        scenario.Two.BoardCards.ShouldBeEmpty();
        events.OfType<PerkTriggered>().ShouldHaveSingleItem().Seat.ShouldBe(Seat.One);
    }

    [Fact]
    public void Abyss_never_keeps_a_decoy()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var scenario = new Scenario(factionOne: Faction.Abyss);
            scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Decoy());
            scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Decoy());
            var unit = scenario.AddToBoard(Seat.One, RowType.Siege, TestCards.Unit(2, RowType.Siege));
            scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(20));

            EndRound(scenario.Build(seed));

            scenario.One.BoardCards.ShouldBe([unit]);
        }
    }

    [Fact]
    public void Abyss_keeps_nothing_when_only_decoys_are_on_the_board()
    {
        var scenario = new Scenario(factionOne: Faction.Abyss);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Decoy());
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(20));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        scenario.One.BoardCards.ShouldBeEmpty();
        events.OfType<PerkTriggered>().ShouldBeEmpty();
    }

    [Fact]
    public void Abyss_keep_is_random_but_deterministic_per_seed()
    {
        int KeptStrength(ulong seed)
        {
            var scenario = new Scenario(factionOne: Faction.Abyss);
            for (var i = 1; i <= 6; i++)
            {
                scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(i));
            }

            var engine = scenario.Build(seed);
            EndRound(engine);
            return scenario.One.BoardCards.Single().Definition.Strength;
        }

        KeptStrength(5).ShouldBe(KeptStrength(5));
        Enumerable.Range(1, 20).Select(s => KeptStrength((ulong)s)).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Perks_do_not_trigger_when_the_match_ends()
    {
        var scenario = new Scenario(factionOne: Faction.Realms, factionTwo: Faction.Abyss);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(2));
        scenario.AddToDeck(Seat.One, TestCards.Unit(3));
        scenario.Two.Gems = 1;
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        engine.IsFinished.ShouldBeTrue();
        events.OfType<PerkTriggered>().ShouldBeEmpty();
        scenario.Two.BoardCards.ShouldBeEmpty();
        scenario.One.Deck.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(Seat.One)]
    [InlineData(Seat.Two)]
    public void Horde_wins_a_tied_round(Seat horde)
    {
        var scenario = new Scenario(
            factionOne: horde == Seat.One ? Faction.Horde : Faction.Realms,
            factionTwo: horde == Seat.Two ? Faction.Horde : Faction.Realms);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(4));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(4));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        var round = events.OfType<RoundEnded>().ShouldHaveSingleItem().Result;
        round.Winner.ShouldBe(horde);
        engine.State.Player(horde).Gems.ShouldBe(2);
        engine.State.Player(horde.Opponent()).Gems.ShouldBe(1);
        events.OfType<PerkTriggered>().ShouldContain(e => e.Seat == horde && e.Faction == Faction.Horde);
    }

    [Fact]
    public void Horde_wins_an_empty_round_too()
    {
        var scenario = new Scenario(factionOne: Faction.Horde);
        var engine = scenario.Build();

        EndRound(engine);

        engine.State.RoundResults.ShouldHaveSingleItem().Winner.ShouldBe(Seat.One);
    }

    [Fact]
    public void A_horde_mirror_tie_is_a_draw()
    {
        var scenario = new Scenario(factionOne: Faction.Horde, factionTwo: Faction.Horde);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(4));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(4));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        events.OfType<RoundEnded>().ShouldHaveSingleItem().Result.Winner.ShouldBeNull();
        events.OfType<PerkTriggered>().ShouldBeEmpty();
        scenario.One.Gems.ShouldBe(1);
        scenario.Two.Gems.ShouldBe(1);
    }

    [Fact]
    public void Horde_does_not_change_a_decided_round()
    {
        var scenario = new Scenario(factionOne: Faction.Horde);
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(1));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        events.OfType<RoundEnded>().ShouldHaveSingleItem().Result.Winner.ShouldBe(Seat.Two);
        events.OfType<PerkTriggered>().ShouldBeEmpty();
    }

    [Fact]
    public void A_horde_tie_win_can_end_the_match()
    {
        var scenario = new Scenario(factionOne: Faction.Realms, factionTwo: Faction.Horde);
        scenario.One.Gems = 1;
        var engine = scenario.Build();

        EndRound(engine);

        engine.IsFinished.ShouldBeTrue();
        engine.State.Outcome!.Winner.ShouldBe(Seat.Two);
    }

    [Theory]
    [InlineData(Faction.Horde, Faction.Realms, Seat.One)]
    [InlineData(Faction.Abyss, Faction.Horde, Seat.Two)]
    [InlineData(Faction.Horde, Faction.Horde, null)]
    [InlineData(Faction.Realms, Faction.Abyss, null)]
    public void Tie_winner_depends_on_both_factions(Faction one, Faction two, Seat? expected) =>
        FactionPerks.TieWinner(new Scenario(one, two).State).ShouldBe(expected);

    [Theory]
    [InlineData(Faction.Realms, "Draw a card after winning a round.")]
    [InlineData(Faction.Abyss, "One random unit stays on the board after each round.")]
    [InlineData(Faction.Horde, "Win the round when the scores are tied.")]
    [InlineData(Faction.Neutral, "No perk.")]
    public void Every_faction_has_a_description(Faction faction, string expected) =>
        FactionPerks.Describe(faction).ShouldBe(expected);
}
