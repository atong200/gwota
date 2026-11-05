using Gwota.Core.Ai;
using Gwota.Core.Presentation;
using Gwota.Core.Session;

namespace Gwota.Core.Tests.Presentation;

public class MatchTextsTests
{
    private readonly Scenario _scenario = new();
    private readonly MatchSelection _selection = new();

    [Theory]
    [InlineData(MatchPhase.Finished, "MATCH OVER")]
    [InlineData(MatchPhase.Draft, "DRAFT PHASE")]
    public void Status_names_the_draft_and_finished_phases(MatchPhase phase, string expected)
    {
        _scenario.State.Phase = phase;

        MatchTexts.Status(Session(), Seat.One, _selection).ShouldBe(new MatchStatus(expected, string.Empty));
    }

    [Fact]
    public void Status_shows_a_controlled_opponent_thinking()
    {
        _scenario.State.ActiveSeat = Seat.Two;
        var session = Session(new Dictionary<Seat, IPlayerController> { [Seat.Two] = new HeuristicAi() });

        MatchTexts.Status(session, Seat.One, _selection).ShouldBe(new MatchStatus("PLAYER TWO IS THINKING...", string.Empty));
    }

    [Fact]
    public void Status_shows_a_remote_opponent_turn()
    {
        _scenario.State.ActiveSeat = Seat.Two;

        MatchTexts.Status(Session(), Seat.One, _selection).ShouldBe(new MatchStatus("ROUND 1 - PLAYER TWO'S TURN", string.Empty));
    }

    [Fact]
    public void Status_on_your_turn_without_a_selection_has_no_hint()
    {
        MatchTexts.Status(Session(), Seat.One, _selection).ShouldBe(new MatchStatus("ROUND 1 - YOUR TURN", string.Empty));
    }

    [Fact]
    public void Status_hints_at_clicking_a_row_for_a_unit()
    {
        var unit = _scenario.AddToHand(Seat.One, TestCards.Unit(4));

        HintAfterSelecting(unit.Id).ShouldBe("Click a highlighted row to play it.\nRight-click to cancel.");
    }

    [Fact]
    public void Status_hints_at_clicking_the_board_for_an_untargeted_special()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Scorch());

        HintAfterSelecting(scorch.Id).ShouldBe("Click anywhere on the board to play it.\nRight-click to cancel.");
    }

    [Fact]
    public void Status_hints_at_clicking_a_card_for_a_decoy()
    {
        var decoy = _scenario.AddToHand(Seat.One, TestCards.Decoy());
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));

        HintAfterSelecting(decoy.Id).ShouldBe("Click a highlighted card to target it.\nRight-click to cancel.");
    }

    [Fact]
    public void Status_hints_at_using_a_selected_board_wide_leader()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.HornRow, RowType.Siege));
        var session = new MatchSession(scenario.Build(), new Dictionary<Seat, IPlayerController>());
        _selection.ClickLeader(session.State, session.GetLegalCommands(Seat.One), Seat.One);

        MatchTexts.Status(session, Seat.One, _selection).Hint
            .ShouldBe("Click the leader again or anywhere on the board to use it.\nRight-click to cancel.");
    }

    [Fact]
    public void Status_hints_at_targeting_a_unit_for_a_return_leader()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.ReturnFromBoard));
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var session = new MatchSession(scenario.Build(), new Dictionary<Seat, IPlayerController>());
        _selection.ClickLeader(session.State, session.GetLegalCommands(Seat.One), Seat.One);

        MatchTexts.Status(session, Seat.One, _selection).Hint
            .ShouldBe("Click a highlighted unit to return it to your hand.\nClick the leader again or right-click to cancel.");
    }

    [Theory]
    [InlineData(Seat.One, "Victory")]
    [InlineData(Seat.Two, "Defeat")]
    [InlineData(null, "Draw")]
    public void End_title_is_seen_from_the_local_seat(Seat? winner, string title)
    {
        Finish(winner);

        MatchTexts.End(_scenario.State, Seat.One, link: null).Title.ShouldBe(title);
    }

    [Fact]
    public void End_of_a_local_match_lists_the_rounds_and_offers_a_rematch()
    {
        Finish(Seat.One);

        MatchTexts.End(_scenario.State, Seat.One, link: null)
            .ShouldBe(new MatchEndText("Victory", "Round 1:  10 - 5\nRound 2:  7 - 7", "Rematch"));
    }

    [Theory]
    [InlineData(Seat.One, "You forfeited the match.")]
    [InlineData(Seat.Two, "Player Two forfeited the match.")]
    public void End_starts_with_the_forfeit_line(Seat quitter, string line)
    {
        Finish(quitter.Opponent(), quitter);

        MatchTexts.End(_scenario.State, Seat.One, link: null).Body.ShouldBe($"{line}\nRound 1:  10 - 5\nRound 2:  7 - 7");
    }

    [Fact]
    public void End_after_the_opponent_left_shows_the_reason_and_no_rematch()
    {
        Finish(Seat.One);

        var end = MatchTexts.End(_scenario.State, Seat.One, new LinkState("Player Two left the match.", OpponentLeft: true, RematchStatus.None));

        end.Body.ShouldEndWith("\nPlayer Two left the match.");
        end.RematchLabel.ShouldBeNull();
    }

    [Fact]
    public void End_after_the_link_closed_otherwise_hides_the_reason_and_the_rematch()
    {
        Finish(Seat.One);

        var end = MatchTexts.End(_scenario.State, Seat.One, new LinkState("Connection lost.", OpponentLeft: false, RematchStatus.None));

        end.Body.ShouldBe("Round 1:  10 - 5\nRound 2:  7 - 7");
        end.RematchLabel.ShouldBeNull();
    }

    [Theory]
    [InlineData(RematchStatus.None, null, "Rematch")]
    [InlineData(RematchStatus.RequestedByYou, "Waiting for Player Two to accept the rematch...", null)]
    [InlineData(RematchStatus.RequestedByOpponent, "Player Two wants a rematch.", "Accept rematch")]
    public void End_of_an_open_link_follows_the_rematch_status(RematchStatus status, string? extraLine, string? label)
    {
        Finish(Seat.Two);

        var end = MatchTexts.End(_scenario.State, Seat.One, new LinkState(null, OpponentLeft: false, status));

        end.Body.ShouldBe(extraLine is null ? "Round 1:  10 - 5\nRound 2:  7 - 7" : $"Round 1:  10 - 5\nRound 2:  7 - 7\n{extraLine}");
        end.RematchLabel.ShouldBe(label);
    }

    [Theory]
    [InlineData(ChoiceKind.MedicRevive, "Revive a unit")]
    [InlineData(ChoiceKind.ReturnFromDiscard, "Return a card to your hand")]
    public void Choice_title_depends_on_the_choice(ChoiceKind kind, string title) =>
        MatchTexts.ChoiceTitle(kind).ShouldBe(title);

    [Fact]
    public void Round_banner_names_the_winner_or_a_draw()
    {
        MatchTexts.RoundBanner(_scenario.State, new RoundResult(2, 10, 5, Seat.One)).ShouldBe("Player One wins round 2");
        MatchTexts.RoundBanner(_scenario.State, new RoundResult(1, 4, 4, null)).ShouldBe("Round drawn");
    }

    [Fact]
    public void Short_texts_are_verbatim()
    {
        MatchTexts.WaitingForDraft("Bob").ShouldBe("Waiting for Bob to finish drafting...");
        MatchTexts.Unplayable(TestCards.Unit(1, id: "axe")).ShouldEndWith(" cannot be played right now.");
    }

    private MatchSession Session(IReadOnlyDictionary<Seat, IPlayerController>? controllers = null) =>
        new(_scenario.Build(), controllers ?? new Dictionary<Seat, IPlayerController>());

    private string HintAfterSelecting(int cardId)
    {
        var session = Session();
        _selection.ClickCard(session.State, session.GetLegalCommands(Seat.One), Seat.One, cardId).ShouldBe(SelectionChange.Selected);
        return MatchTexts.Status(session, Seat.One, _selection).Hint;
    }

    private void Finish(Seat? winner, Seat? forfeitedBy = null)
    {
        _scenario.State.Phase = MatchPhase.Finished;
        _scenario.State.RoundResultList.Add(new RoundResult(1, 10, 5, Seat.One));
        _scenario.State.RoundResultList.Add(new RoundResult(2, 7, 7, null));
        _scenario.State.Outcome = new MatchOutcome(winner, forfeitedBy);
    }

    [Fact]
    public void DiscardPile_lists_the_most_recent_card_first()
    {
        var first = _scenario.AddToDiscard(Seat.One, TestCards.Unit(1));
        var second = _scenario.AddToDiscard(Seat.One, TestCards.Weather(RowType.Melee));
        var last = _scenario.AddToDiscard(Seat.One, TestCards.Unit(3));

        var pile = MatchTexts.DiscardPile(_scenario.State, Seat.One, Seat.One);

        pile.Cards.ShouldBe([last, second, first]);
        pile.Title.ShouldBe("Your discard pile (3)");
        _scenario.One.Discard.ShouldBe([first, second, last]);
    }

    [Fact]
    public void DiscardPile_names_the_opponent()
    {
        var card = _scenario.AddToDiscard(Seat.Two, TestCards.Unit(2));

        var pile = MatchTexts.DiscardPile(_scenario.State, Seat.Two, Seat.One);

        pile.Title.ShouldBe($"{_scenario.Two.Name}'s discard pile (1)");
        pile.Cards.ShouldBe([card]);
    }

    [Theory]
    [InlineData(Seat.One, "Your discard pile (0)", "No cards in your discard pile yet.")]
    [InlineData(Seat.Two, "Player Two's discard pile (0)", "No cards in Player Two's discard pile yet.")]
    public void DiscardPile_can_be_empty(Seat owner, string title, string emptyText)
    {
        var pile = MatchTexts.DiscardPile(_scenario.State, owner, Seat.One);

        pile.Title.ShouldBe(title);
        pile.EmptyText.ShouldBe(emptyText);
        pile.Cards.ShouldBeEmpty();
    }
}
