using Gwota.Core.Ai;
using Gwota.Core.Session;

namespace Gwota.Core.Tests.Session;

public class MatchSessionTests
{
    private static readonly IReadOnlyDictionary<Seat, IPlayerController> AiOnSeatTwo =
        new Dictionary<Seat, IPlayerController> { [Seat.Two] = new HeuristicAi() };

    private static MatchSession NewSession(IReadOnlyDictionary<Seat, IPlayerController> controllers, ulong seed = 7) =>
        new(MatchRunner.NewMatch(TestDecks.Mixed(Faction.Realms), TestDecks.Mixed(Faction.Hells), seed), controllers);

    [Fact]
    public void Human_acts_first_during_the_draft_and_controllers_wait()
    {
        var session = NewSession(AiOnSeatTwo);

        session.HumanToAct.ShouldBe(Seat.One);
        session.IsControllerTurn.ShouldBeFalse();
        session.StepController().ShouldBeNull();
    }

    [Fact]
    public void History_starts_with_the_engine_initial_events()
    {
        var session = NewSession(AiOnSeatTwo);

        session.History.ShouldBe(session.Engine.InitialEvents);
    }

    [Fact]
    public void Submit_rejects_commands_for_controlled_seats()
    {
        var session = NewSession(AiOnSeatTwo);

        var result = session.Submit(FinishDraftCommand.KeepHand(Seat.Two));

        result.IsSuccess.ShouldBeFalse();
        session.State.Player(Seat.Two).HasFinishedDraft.ShouldBeFalse();
    }

    [Fact]
    public void A_human_can_forfeit_during_the_draft_and_the_opponent_wins()
    {
        var session = NewSession(AiOnSeatTwo);

        var result = session.Submit(new ForfeitCommand(Seat.One));

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldBe([new PlayerForfeited(Seat.One), new MatchEnded(new MatchOutcome(Seat.Two, ForfeitedBy: Seat.One))]);
        session.IsFinished.ShouldBeTrue();
        session.State.Outcome.ShouldBe(new MatchOutcome(Seat.Two, ForfeitedBy: Seat.One));
        session.IsControllerTurn.ShouldBeFalse();
    }

    [Fact]
    public void A_human_can_forfeit_while_the_controller_is_to_act()
    {
        var session = NewSession(AiOnSeatTwo);
        session.Submit(FinishDraftCommand.KeepHand(Seat.One));
        session.IsControllerTurn.ShouldBeTrue();

        session.Submit(new ForfeitCommand(Seat.One)).IsSuccess.ShouldBeTrue();

        session.State.Outcome!.Winner.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Forfeiting_a_finished_match_fails()
    {
        var session = NewSession(AiOnSeatTwo);
        session.Submit(new ForfeitCommand(Seat.One));

        session.Submit(new ForfeitCommand(Seat.One)).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Forfeit_is_never_a_legal_command_so_the_ai_cannot_choose_it()
    {
        var session = NewSession(AiOnSeatTwo);

        session.Engine.GetLegalCommands(Seat.One).OfType<ForfeitCommand>().ShouldBeEmpty();
        session.Engine.GetLegalCommands(Seat.Two).OfType<ForfeitCommand>().ShouldBeEmpty();
    }

    [Fact]
    public void Illegal_submissions_fail_without_recording_events()
    {
        var session = NewSession(AiOnSeatTwo);
        var raised = 0;
        session.EventsApplied += (_, _) => raised++;
        var before = session.History.Count;

        session.Submit(new PassCommand(Seat.One)).IsSuccess.ShouldBeFalse();

        session.History.Count.ShouldBe(before);
        raised.ShouldBe(0);
    }

    [Fact]
    public void Successful_commands_raise_events_and_extend_history()
    {
        var session = NewSession(AiOnSeatTwo);
        IReadOnlyList<GameEvent>? received = null;
        session.EventsApplied += (_, events) => received = events;

        var result = session.Submit(FinishDraftCommand.KeepHand(Seat.One));

        result.IsSuccess.ShouldBeTrue();
        received.ShouldBe(result.Events);
        session.History.TakeLast(result.Events.Count).ShouldBe(result.Events);
    }

    [Fact]
    public void Controllers_run_until_the_human_must_act()
    {
        var session = NewSession(AiOnSeatTwo);
        session.Submit(FinishDraftCommand.KeepHand(Seat.One));

        session.IsControllerTurn.ShouldBeTrue();
        session.RunControllers().ShouldBeGreaterThan(0);

        (session.HumanToAct == Seat.One || session.IsFinished).ShouldBeTrue();
        session.IsControllerTurn.ShouldBeFalse();
    }

    [Fact]
    public void A_fully_controlled_session_plays_to_the_end()
    {
        var both = new Dictionary<Seat, IPlayerController> { [Seat.One] = new HeuristicAi(), [Seat.Two] = new HeuristicAi() };
        var session = NewSession(both);

        session.RunControllers();

        session.IsFinished.ShouldBeTrue();
        session.HumanToAct.ShouldBeNull();
        session.History.OfType<MatchEnded>().ShouldHaveSingleItem();
    }

    [Fact]
    public void A_controller_returning_an_illegal_command_is_a_bug()
    {
        var session = NewSession(new Dictionary<Seat, IPlayerController> { [Seat.One] = new PassingController() });

        Should.Throw<InvalidOperationException>(() => session.StepController());
    }

    [Fact]
    public void Play_options_list_every_row_of_an_agile_unit()
    {
        var scenario = new Scenario();
        var agile = scenario.AddToHand(Seat.One, TestCards.Agile(4, "agile", RowType.Melee, RowType.Ranged));
        var session = new MatchSession(scenario.Build(), new Dictionary<Seat, IPlayerController>());

        session.PlayOptions(Seat.One, agile.Id).Select(c => c.Row).ShouldBe([RowType.Melee, RowType.Ranged], ignoreOrder: true);
    }

    [Fact]
    public void Play_options_list_decoy_targets()
    {
        var scenario = new Scenario();
        var decoy = scenario.AddToHand(Seat.One, TestCards.Decoy());
        var target = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(8));
        var session = new MatchSession(scenario.Build(), new Dictionary<Seat, IPlayerController>());

        session.PlayOptions(Seat.One, decoy.Id).Select(c => c.TargetId).ShouldBe([target.Id]);
    }

    [Fact]
    public void Is_legal_mirrors_the_engine()
    {
        var session = NewSession(AiOnSeatTwo);

        session.IsLegal(FinishDraftCommand.KeepHand(Seat.One)).ShouldBeTrue();
        session.IsLegal(new PassCommand(Seat.One)).ShouldBeFalse();
    }

    private sealed class PassingController : IPlayerController
    {
        public GameCommand Decide(MatchEngine engine, Seat seat) => new PassCommand(seat);
    }

    [Fact]
    public void Both_seats_can_act_during_the_draft_until_they_finish()
    {
        var session = NewSession(new Dictionary<Seat, IPlayerController>());

        session.CanAct(Seat.One).ShouldBeTrue();
        session.CanAct(Seat.Two).ShouldBeTrue();

        session.Submit(FinishDraftCommand.KeepHand(Seat.Two));

        session.CanAct(Seat.One).ShouldBeTrue();
        session.CanAct(Seat.Two).ShouldBeFalse();
    }

    [Fact]
    public void Only_the_seat_to_act_can_act_while_playing()
    {
        var session = NewSession(new Dictionary<Seat, IPlayerController>());
        session.Submit(FinishDraftCommand.KeepHand(Seat.One));
        session.Submit(FinishDraftCommand.KeepHand(Seat.Two));

        var toAct = session.State.SeatToAct!.Value;

        session.CanAct(toAct).ShouldBeTrue();
        session.CanAct(toAct.Opponent()).ShouldBeFalse();
    }

    [Fact]
    public void Nobody_can_act_once_the_match_is_over()
    {
        var session = NewSession(new Dictionary<Seat, IPlayerController>());
        session.Submit(new ForfeitCommand(Seat.One));

        session.CanAct(Seat.One).ShouldBeFalse();
        session.CanAct(Seat.Two).ShouldBeFalse();
    }}
