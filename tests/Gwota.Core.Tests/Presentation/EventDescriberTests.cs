using Gwota.Core.Presentation;

namespace Gwota.Core.Tests.Presentation;

public class EventDescriberTests
{
    private static readonly Scenario Table = new();

    private static string? Describe(GameEvent e, Seat viewer = Seat.One) => EventDescriber.Describe(e, Table.State, viewer);

    private static CardInstance Card(string name, Seat owner = Seat.One) =>
        new(99, TestCards.Unit(5) with { Name = name }, owner);

    [Fact]
    public void Forfeits_are_logged_and_the_win_says_by_forfeit()
    {
        Describe(new PlayerForfeited(Seat.One)).ShouldBe("Player One forfeits.");
        Describe(new MatchEnded(new MatchOutcome(Seat.Two, ForfeitedBy: Seat.One))).ShouldBe("Player Two wins the match by forfeit.");
        Describe(new MatchEnded(new MatchOutcome(Seat.Two))).ShouldBe("Player Two wins the match!");
    }

    [Fact]
    public void Plays_mention_player_card_and_row() =>
        Describe(new CardPlayed(Seat.One, Card("Sven"), Seat.One, RowType.Melee))
            .ShouldBe("Player One plays Sven on the melee row.");

    [Fact]
    public void Spies_mention_the_side_they_land_on() =>
        Describe(new CardPlayed(Seat.Two, Card("Riki", Seat.Two), Seat.One, RowType.Melee))
            .ShouldBe("Player Two sends Riki to Player One's melee row.");

    [Fact]
    public void Specials_without_rows_omit_the_row() =>
        Describe(new CardPlayed(Seat.One, Card("Laguna Blade"), Seat.One, null)).ShouldBe("Player One plays Laguna Blade.");

    [Fact]
    public void Own_draws_reveal_card_names_but_opponent_draws_do_not()
    {
        var scenario = new Scenario();
        var drawnByOne = scenario.AddToHand(Seat.One, TestCards.Unit(3) with { Name = "Lina" });
        var drawnByTwo = scenario.AddToHand(Seat.Two, TestCards.Unit(3) with { Name = "Viper" });

        EventDescriber.Describe(new CardsDrawn(Seat.One, [drawnByOne.Id]), scenario.State, Seat.One)
            .ShouldBe("Player One draws 1 card (Lina).");
        EventDescriber.Describe(new CardsDrawn(Seat.Two, [drawnByTwo.Id, drawnByTwo.Id]), scenario.State, Seat.One)
            .ShouldBe("Player Two draws 2 cards.");
    }

    [Fact]
    public void Draft_lines_reveal_swapped_cards_only_to_their_owner()
    {
        var scenario = new Scenario();
        var returned = scenario.AddToDeck(Seat.One, TestCards.Unit(1) with { Name = "Creep" });
        var drawn = scenario.AddToHand(Seat.One, TestCards.Unit(9) with { Name = "Sven" });
        var draft = new DraftFinished(Seat.One, [returned.Id], [drawn.Id]);

        EventDescriber.Describe(draft, scenario.State, Seat.One).ShouldBe("Player One redraws 1 card (Creep) -> (Sven).");
        EventDescriber.Describe(draft, scenario.State, Seat.Two).ShouldBe("Player One redraws 1 card.");
    }

    [Fact]
    public void Keeping_the_starting_hand_is_logged() =>
        Describe(new DraftFinished(Seat.Two, [], [])).ShouldBe("Player Two keeps their starting hand.");

    [Fact]
    public void Empty_draws_and_turn_changes_are_not_logged()
    {
        Describe(new CardsDrawn(Seat.One, [])).ShouldBeNull();
        Describe(new TurnStarted(Seat.One)).ShouldBeNull();
    }

    [Theory]
    [InlineData(Seat.One, "Round 2 ends 30 - 12: Player One wins the round.")]
    [InlineData(null, "Round 2 ends 30 - 12: a draw.")]
    public void Round_results_show_scores_and_winner(Seat? winner, string expected) =>
        Describe(new RoundEnded(new RoundResult(2, 30, 12, winner))).ShouldBe(expected);

    [Theory]
    [InlineData(Seat.Two, "Player Two wins the match!")]
    [InlineData(null, "The match is a draw.")]
    public void Match_end_announces_the_winner(Seat? winner, string expected) =>
        Describe(new MatchEnded(new MatchOutcome(winner))).ShouldBe(expected);

    [Fact]
    public void Automatic_passes_explain_why() =>
        Describe(new PlayerPassed(Seat.One, Automatic: true)).ShouldBe("Player One has no cards left and passes.");

    [Fact]
    public void Destroyed_and_mustered_cards_are_listed()
    {
        Describe(new CardsDestroyed([Card("Axe"), Card("Doom")])).ShouldBe("Destroyed: Axe, Doom.");
        Describe(new CardsMustered(Seat.One, [Card("Creep"), Card("Creep")])).ShouldBe("Player One musters Creep, Creep.");
    }

    [Fact]
    public void Every_event_of_real_matches_is_described_without_throwing()
    {
        var realms = TestDecks.Mixed(Faction.Realms);
        var hells = TestDecks.Mixed(Faction.Hells);

        for (ulong seed = 1; seed <= 10; seed++)
        {
            var run = MatchRunner.Run(realms, hells, seed);
            var engine = MatchRunner.NewMatch(realms, hells, seed);
            var events = engine.InitialEvents.Concat(run.Commands.SelectMany(engine.ApplyOk)).ToList();

            events.ShouldAllBe(e => DescribeSafely(e, engine.State));
        }
    }

    private static bool DescribeSafely(GameEvent e, MatchState state)
    {
        _ = EventDescriber.Describe(e, state, Seat.One);
        return true;
    }
}
