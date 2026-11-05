using Gwota.Core.Ai;
using Gwota.Core.Presentation;
using Gwota.Core.Session;

namespace Gwota.Core.Tests.Presentation;

public class MatchSelectionTests
{
    private readonly MatchSelection _selection = new();

    [Fact]
    public void Starts_empty()
    {
        _selection.IsEmpty.ShouldBeTrue();
        _selection.CardId.ShouldBeNull();
        _selection.IsLeader.ShouldBeFalse();
        _selection.Targets.ShouldBeSameAs(PlayTargets.None);
        _selection.Clear().ShouldBeFalse();
    }

    [Fact]
    public void Clicking_a_playable_card_selects_it_and_its_targets()
    {
        var scenario = new Scenario();
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(4, RowType.Ranged));
        var session = Session(scenario);

        Click(session, unit.Id).ShouldBe(SelectionChange.Selected);

        _selection.CardId.ShouldBe(unit.Id);
        _selection.IsLeader.ShouldBeFalse();
        _selection.Targets.Rows.ShouldBe([(Seat.One, RowType.Ranged)]);
    }

    [Fact]
    public void Clicking_the_selected_card_again_clears_the_selection()
    {
        var scenario = new Scenario();
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(4));
        var session = Session(scenario);
        Click(session, unit.Id);

        Click(session, unit.Id).ShouldBe(SelectionChange.Cleared);

        _selection.IsEmpty.ShouldBeTrue();
        _selection.Targets.ShouldBeSameAs(PlayTargets.None);
    }

    [Fact]
    public void Clicking_another_card_switches_the_selection()
    {
        var scenario = new Scenario();
        var first = scenario.AddToHand(Seat.One, TestCards.Unit(4));
        var second = scenario.AddToHand(Seat.One, TestCards.Unit(2, RowType.Siege));
        var session = Session(scenario);
        Click(session, first.Id);

        Click(session, second.Id).ShouldBe(SelectionChange.Selected);

        _selection.CardId.ShouldBe(second.Id);
        _selection.Targets.Rows.ShouldBe([(Seat.One, RowType.Siege)]);
    }

    [Fact]
    public void Clicking_an_unplayable_card_keeps_the_current_selection()
    {
        var scenario = new Scenario();
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(4));
        var decoy = scenario.AddToHand(Seat.One, TestCards.Decoy());
        var session = Session(scenario);
        Click(session, unit.Id);

        Click(session, decoy.Id).ShouldBe(SelectionChange.Unplayable);

        _selection.CardId.ShouldBe(unit.Id);
    }

    [Fact]
    public void A_board_wide_leader_is_selected_then_used_by_a_second_click()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.HornRow, RowType.Siege));
        var session = Session(scenario);

        ClickLeader(session).ShouldBe(SelectionChange.Selected);
        _selection.IsLeader.ShouldBeTrue();
        _selection.CardId.ShouldBeNull();
        _selection.Targets.IsBoardWide.ShouldBeTrue();

        ClickLeader(session).ShouldBe(SelectionChange.Use);
        _selection.IsLeader.ShouldBeTrue();
        _selection.Targets.ForBoard().ShouldBe(new UseLeaderCommand(Seat.One));
    }

    [Fact]
    public void A_targeted_leader_is_cancelled_by_a_second_click()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.ReturnFromBoard));
        var unit = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var session = Session(scenario);

        ClickLeader(session).ShouldBe(SelectionChange.Selected);
        _selection.Targets.Cards.ShouldBe([unit.Id]);

        ClickLeader(session).ShouldBe(SelectionChange.Cleared);
        _selection.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void An_unusable_leader_cannot_be_selected()
    {
        var session = Session(new Scenario());

        ClickLeader(session).ShouldBe(SelectionChange.Unplayable);

        _selection.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Selecting_the_leader_replaces_a_selected_card()
    {
        var scenario = new Scenario(leaderOne: TestCards.Leader(LeaderAbility.HornRow, RowType.Siege));
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(4));
        var session = Session(scenario);
        Click(session, unit.Id);

        ClickLeader(session);

        _selection.CardId.ShouldBeNull();
        _selection.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public void Refresh_recomputes_the_targets_of_a_still_playable_card()
    {
        var scenario = new Scenario();
        var horn = scenario.AddToHand(Seat.One, TestCards.Horn());
        var session = Session(scenario);
        Click(session, horn.Id);
        scenario.SetHorn(Seat.One, RowType.Melee);

        _selection.Refresh(session.State, LegalCommands.For(session.State, Seat.One), Seat.One);

        _selection.CardId.ShouldBe(horn.Id);
        _selection.Targets.Rows.ShouldBe([(Seat.One, RowType.Ranged), (Seat.One, RowType.Siege)], ignoreOrder: true);
    }

    [Fact]
    public void Refresh_drops_a_selection_that_can_no_longer_be_played()
    {
        var scenario = new Scenario();
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(4));
        var session = Session(scenario);
        Click(session, unit.Id);

        _selection.Refresh(session.State, [], Seat.One);

        _selection.IsEmpty.ShouldBeTrue();
        _selection.Targets.ShouldBeSameAs(PlayTargets.None);
    }

    [Fact]
    public void Clear_reports_whether_something_was_selected()
    {
        var scenario = new Scenario();
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(4));
        Click(Session(scenario), unit.Id);

        _selection.Clear().ShouldBeTrue();
        _selection.IsEmpty.ShouldBeTrue();
        _selection.Clear().ShouldBeFalse();
    }

    private static MatchSession Session(Scenario scenario) => new(scenario.Build(), new Dictionary<Seat, IPlayerController>());

    private SelectionChange Click(MatchSession session, int cardId) =>
        _selection.ClickCard(session.State, session.GetLegalCommands(Seat.One), Seat.One, cardId);

    private SelectionChange ClickLeader(MatchSession session) =>
        _selection.ClickLeader(session.State, session.GetLegalCommands(Seat.One), Seat.One);
}
