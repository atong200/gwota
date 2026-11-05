namespace Gwota.Core.Tests.Match;

public class MatchSetupTests
{
    private static PlayerSetup Setup(string name, int deckSize = 25) =>
        new(name, Faction.Neutral, TestCards.Leader(LeaderAbility.ClearWeather),
            Enumerable.Range(1, deckSize).Select(i => TestCards.Unit(i % 10, id: $"card_{i}")).ToList());

    private static MatchEngine NewMatch(ulong seed = 1, MatchRules? rules = null) =>
        MatchEngine.Create(Setup("One"), Setup("Two"), new SeededRandom(seed), rules);

    [Fact]
    public void Create_deals_starting_hands_and_starts_in_the_draft_phase()
    {
        var engine = NewMatch();

        engine.State.Phase.ShouldBe(MatchPhase.Draft);
        engine.State.Round.ShouldBe(1);
        foreach (var player in engine.State.Players)
        {
            player.Hand.Count.ShouldBe(10);
            player.Deck.Count.ShouldBe(15);
            player.Gems.ShouldBe(2);
            player.RedrawLimit.ShouldBe(2);
            player.HandLimit.ShouldBe(20);
            player.HasFinishedDraft.ShouldBeFalse();
            player.Hand.ShouldAllBe(c => c.Owner == player.Seat);
        }

        engine.InitialEvents.OfType<MatchStarted>().ShouldHaveSingleItem().FirstPlayer.ShouldBe(engine.State.RoundStarter);
        engine.InitialEvents.OfType<CardsDrawn>().Count().ShouldBe(2);
    }

    [Fact]
    public void Card_instance_ids_are_unique()
    {
        var engine = NewMatch();

        var ids = engine.State.Players.SelectMany(p => p.Hand.Concat(p.Deck)).Select(c => c.Id).ToList();
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void Copies_of_a_card_are_numbered_in_each_deck()
    {
        var sven = TestCards.Unit(5, id: "sven");
        var deck = new[] { sven, TestCards.Unit(3, id: "other"), sven, sven }
            .Concat(Enumerable.Range(1, 21).Select(i => TestCards.Unit(1, id: $"card_{i}"))).ToList();
        var setup = new PlayerSetup("One", Faction.Neutral, TestCards.Leader(LeaderAbility.ClearWeather), deck);

        var engine = MatchEngine.Create(setup, setup with { Name = "Two" }, new SeededRandom(3));

        foreach (var player in engine.State.Players)
        {
            var cards = player.Hand.Concat(player.Deck).ToList();
            cards.Where(c => c.Definition.Id == "sven").Select(c => c.Copy).ShouldBe([0, 1, 2], ignoreOrder: true);
            cards.Where(c => c.Definition.Id != "sven").ShouldAllBe(c => c.Copy == 0);
        }
    }
    [Fact]
    public void Same_seed_produces_the_same_match()
    {
        var a = NewMatch(99);
        var b = NewMatch(99);

        a.State.Player(Seat.One).Hand.Select(c => c.Definition.Id).ShouldBe(b.State.Player(Seat.One).Hand.Select(c => c.Definition.Id));
        a.State.RoundStarter.ShouldBe(b.State.RoundStarter);
    }

    [Fact]
    public void Rules_are_configurable()
    {
        var engine = NewMatch(rules: MatchRules.Classic with { StartingHandSize = 5, StartingGems = 3 });

        engine.State.Player(Seat.One).Hand.Count.ShouldBe(5);
        engine.State.Player(Seat.Two).Gems.ShouldBe(3);
    }

    [Fact]
    public void Starting_hand_is_capped_by_the_hand_limit()
    {
        var engine = NewMatch(rules: MatchRules.Classic with { StartingHandSize = 12, MaxHandSize = 8 });

        foreach (var player in engine.State.Players)
        {
            player.HandLimit.ShouldBe(8);
            player.IsHandFull.ShouldBeTrue();
            player.Hand.Count.ShouldBe(8);
            player.Deck.Count.ShouldBe(17);
        }
    }
}
