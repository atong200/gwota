namespace Gwota.Core.Tests.Match;

public class DraftPhaseTests
{
    private static PlayerSetup Setup(string name, int deckSize) =>
        new(name, Faction.Neutral, TestCards.Leader(LeaderAbility.ClearWeather),
            Enumerable.Range(1, deckSize).Select(i => TestCards.Unit(i % 10, id: $"card_{i}")).ToList());

    private static MatchEngine NewMatch(int deckSize = 25, ulong seed = 1, MatchRules? rules = null) =>
        MatchEngine.Create(Setup("One", deckSize), Setup("Two", deckSize), new SeededRandom(seed), rules);

    [Fact]
    public void Keeping_the_hand_changes_nothing()
    {
        var engine = NewMatch();
        var player = engine.State.Player(Seat.One);
        var hand = player.Hand.ToList();
        var deck = player.Deck.ToList();

        var events = engine.ApplyOk(FinishDraftCommand.KeepHand(Seat.One));

        player.Hand.ShouldBe(hand);
        player.Deck.ShouldBe(deck);
        player.HasFinishedDraft.ShouldBeTrue();
        var finished = events.OfType<DraftFinished>().ShouldHaveSingleItem();
        finished.ReturnedCardIds.ShouldBeEmpty();
        finished.DrawnCardIds.ShouldBeEmpty();
    }

    [Fact]
    public void Redrawn_cards_are_replaced_in_place_by_the_top_of_the_deck()
    {
        var engine = NewMatch();
        var player = engine.State.Player(Seat.One);
        var first = player.Hand[2];
        var second = player.Hand[7];
        var (topOne, topTwo) = (player.Deck[0], player.Deck[1]);

        var events = engine.ApplyOk(new FinishDraftCommand(Seat.One, [second.Id, first.Id]));

        player.Hand[2].ShouldBe(topOne);
        player.Hand[7].ShouldBe(topTwo);
        player.Hand.Count.ShouldBe(10);
        player.Deck.Count.ShouldBe(15);
        player.Deck.ShouldContain(first);
        player.Deck.ShouldContain(second);
        var finished = events.OfType<DraftFinished>().ShouldHaveSingleItem();
        finished.ReturnedCardIds.ShouldBe([first.Id, second.Id]);
        finished.DrawnCardIds.ShouldBe([topOne.Id, topTwo.Id]);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void Returned_cards_are_never_drawn_back(ulong seed)
    {
        var engine = NewMatch(deckSize: 12, seed: seed);
        var player = engine.State.Player(Seat.One);
        var returned = player.Hand.Take(2).ToList();
        var remainingDeck = player.Deck.ToList();

        engine.ApplyOk(new FinishDraftCommand(Seat.One, returned.Select(c => c.Id).ToList()));

        player.Hand.ShouldNotContain(returned[0]);
        player.Hand.ShouldNotContain(returned[1]);
        player.Hand.ShouldContain(remainingDeck[0]);
        player.Hand.ShouldContain(remainingDeck[1]);
        player.Deck.ShouldBe(returned, ignoreOrder: true);
    }

    [Fact]
    public void Every_selection_of_up_to_two_cards_is_legal()
    {
        var engine = NewMatch();

        // 1 (keep) + 10 (one card) + 45 (two cards)
        engine.GetLegalCommands(Seat.One).Count.ShouldBe(56);
        engine.GetLegalCommands(Seat.One).ShouldAllBe(c => c is FinishDraftCommand);
    }

    [Fact]
    public void Redrawing_more_than_the_limit_is_rejected()
    {
        var engine = NewMatch();
        var hand = engine.State.Player(Seat.One).Hand;

        engine.Apply(new FinishDraftCommand(Seat.One, [hand[0].Id, hand[1].Id, hand[2].Id])).IsSuccess.ShouldBeFalse();
        engine.State.Player(Seat.One).HasFinishedDraft.ShouldBeFalse();
    }

    [Fact]
    public void Redrawing_the_same_card_twice_is_rejected()
    {
        var engine = NewMatch();
        var card = engine.State.Player(Seat.One).Hand[0];

        engine.Apply(new FinishDraftCommand(Seat.One, [card.Id, card.Id])).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Only_cards_from_the_own_hand_can_be_redrawn()
    {
        var engine = NewMatch();
        var opponentCard = engine.State.Player(Seat.Two).Hand[0];
        var deckCard = engine.State.Player(Seat.One).Deck[0];

        engine.Apply(new FinishDraftCommand(Seat.One, [opponentCard.Id])).IsSuccess.ShouldBeFalse();
        engine.Apply(new FinishDraftCommand(Seat.One, [deckCard.Id])).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Redraws_are_limited_by_the_deck_size()
    {
        var engine = NewMatch(deckSize: 11);
        var hand = engine.State.Player(Seat.One).Hand;

        engine.GetLegalCommands(Seat.One).Count.ShouldBe(11);
        engine.Apply(new FinishDraftCommand(Seat.One, [hand[0].Id, hand[1].Id])).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Redraw_limit_is_configurable()
    {
        var engine = NewMatch(rules: MatchRules.Classic with { MaxRedraws = 0 });

        engine.GetLegalCommands(Seat.One).ShouldBe([FinishDraftCommand.KeepHand(Seat.One)]);
    }

    [Fact]
    public void Selection_order_does_not_matter()
    {
        var a = new FinishDraftCommand(Seat.One, [3, 8]);
        var b = new FinishDraftCommand(Seat.One, [8, 3]);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(new FinishDraftCommand(Seat.Two, [3, 8]));
        a.ShouldNotBe(new FinishDraftCommand(Seat.One, [3]));
        a.ShouldNotBe(new FinishDraftCommand(Seat.One, [3, 3]));
        a.ToString().ShouldContain("3, 8");
    }

    [Fact]
    public void Playing_starts_once_both_players_finish_the_draft()
    {
        var engine = NewMatch();
        var starter = engine.State.RoundStarter;

        engine.ApplyOk(FinishDraftCommand.KeepHand(Seat.One));
        engine.State.Phase.ShouldBe(MatchPhase.Draft);
        engine.State.SeatToAct.ShouldBe(Seat.Two);
        engine.GetLegalCommands(Seat.One).ShouldBeEmpty();

        var events = engine.ApplyOk(FinishDraftCommand.KeepHand(Seat.Two));

        engine.State.Phase.ShouldBe(MatchPhase.Playing);
        engine.State.ActiveSeat.ShouldBe(starter);
        events.OfType<RoundStarted>().ShouldHaveSingleItem().ShouldBe(new RoundStarted(1, starter));
        events.OfType<TurnStarted>().ShouldHaveSingleItem().Seat.ShouldBe(starter);
    }

    [Fact]
    public void Playing_commands_are_rejected_during_the_draft()
    {
        var engine = NewMatch();
        var card = engine.State.Player(Seat.One).Hand[0];

        engine.Apply(new PlayCardCommand(Seat.One, card.Id, RowType.Melee)).IsSuccess.ShouldBeFalse();
        engine.Apply(new PassCommand(Seat.One)).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Clones_keep_the_draft_state()
    {
        var engine = NewMatch();
        engine.ApplyOk(FinishDraftCommand.KeepHand(Seat.One));

        var clone = engine.Clone();

        clone.State.Player(Seat.One).HasFinishedDraft.ShouldBeTrue();
        clone.State.Player(Seat.Two).RedrawLimit.ShouldBe(2);
    }
}
