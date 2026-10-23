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
    public void Hells_keeps_exactly_one_of_its_units_on_the_board()
    {
        var scenario = new Scenario(factionOne: Faction.Hells);
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
    public void Hells_keep_is_random_but_deterministic_per_seed()
    {
        int KeptStrength(ulong seed)
        {
            var scenario = new Scenario(factionOne: Faction.Hells);
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
        var scenario = new Scenario(factionOne: Faction.Realms, factionTwo: Faction.Hells);
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

    [Fact]
    public void Every_faction_has_a_description()
    {
        FactionPerks.Describe(Faction.Realms).ShouldNotBeNullOrWhiteSpace();
        FactionPerks.Describe(Faction.Hells).ShouldNotBeNullOrWhiteSpace();
        FactionPerks.Describe(Faction.Neutral).ShouldNotBeNullOrWhiteSpace();
    }
}
