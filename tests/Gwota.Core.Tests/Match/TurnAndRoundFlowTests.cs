namespace Gwota.Core.Tests.Match;

public class TurnAndRoundFlowTests
{
    [Fact]
    public void Illegal_commands_are_rejected_without_changing_state()
    {
        var scenario = new Scenario();
        var card = scenario.AddToHand(Seat.One, TestCards.Unit(5));
        var engine = scenario.Build();

        engine.Apply(new PlayCardCommand(Seat.Two, card.Id, RowType.Melee)).IsSuccess.ShouldBeFalse();
        engine.Apply(new PlayCardCommand(Seat.One, 9999, RowType.Melee)).IsSuccess.ShouldBeFalse();
        engine.Apply(new PlayCardCommand(Seat.One, card.Id, RowType.Siege)).IsSuccess.ShouldBeFalse();
        engine.Apply(new PassCommand(Seat.Two)).IsSuccess.ShouldBeFalse();
        engine.Apply(new UseLeaderCommand(Seat.One)).IsSuccess.ShouldBeFalse();

        var result = engine.Apply(new ResolveChoiceCommand(Seat.One, card.Id));
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        scenario.One.Hand.ShouldContain(card);
        engine.State.ActiveSeat.ShouldBe(Seat.One);
    }

    [Fact]
    public void Turns_alternate_between_players()
    {
        var scenario = new Scenario();
        var a = scenario.AddToHand(Seat.One, TestCards.Unit(5));
        var b = scenario.AddToHand(Seat.Two, TestCards.Unit(4));
        var engine = scenario.Build();

        engine.Play(a);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
        engine.Play(b);
        engine.State.ActiveSeat.ShouldBe(Seat.One);
    }

    [Fact]
    public void After_a_pass_the_other_player_keeps_playing()
    {
        var scenario = new Scenario();
        var a = scenario.AddToHand(Seat.Two, TestCards.Unit(4));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        engine.Play(a);

        engine.State.ActiveSeat.ShouldBe(Seat.Two);
        engine.GetLegalCommands(Seat.One).ShouldBeEmpty();
    }

    [Fact]
    public void Player_with_nothing_to_play_passes_automatically()
    {
        var scenario = new Scenario(withFillers: false);
        var a = scenario.AddToHand(Seat.One, TestCards.Unit(5));
        scenario.AddToHand(Seat.One, TestCards.Unit(1));
        var engine = scenario.Build();

        var events = engine.Play(a);

        scenario.Two.HasPassed.ShouldBeTrue();
        events.OfType<PlayerPassed>().ShouldHaveSingleItem().ShouldBe(new PlayerPassed(Seat.Two, Automatic: true));
        engine.State.ActiveSeat.ShouldBe(Seat.One);
    }

    [Fact]
    public void Round_ends_when_both_players_pass_and_the_loser_loses_a_gem()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(3));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        engine.State.RoundResults.ShouldBe([new RoundResult(1, 5, 3, Seat.One)]);
        scenario.One.Gems.ShouldBe(2);
        scenario.Two.Gems.ShouldBe(1);
        engine.State.Round.ShouldBe(2);
        engine.State.Phase.ShouldBe(MatchPhase.Playing);
        scenario.One.HasPassed.ShouldBeFalse();
        scenario.Two.HasPassed.ShouldBeFalse();
        events.OfType<RoundEnded>().ShouldHaveSingleItem();
        events.OfType<RoundStarted>().ShouldHaveSingleItem().Round.ShouldBe(2);
    }

    [Fact]
    public void Board_horns_and_weather_are_cleared_to_owners_discards_between_rounds()
    {
        var scenario = new Scenario();
        var unit = scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var spy = scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Spy(), owner: Seat.Two);
        var horn = scenario.SetHorn(Seat.One, RowType.Melee);
        var weather = scenario.AddWeather(TestCards.Weather(RowType.Siege), Seat.Two);
        scenario.One.Row(RowType.Siege).HasLeaderHorn = true;
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        engine.Pass(Seat.Two);

        scenario.One.BoardCards.ShouldBeEmpty();
        scenario.One.Rows.ShouldAllBe(r => !r.HasHornSlotFilled);
        engine.State.Weather.ShouldBeEmpty();
        scenario.One.Discard.ShouldBe([unit, horn], ignoreOrder: true);
        scenario.Two.Discard.ShouldBe([spy, weather], ignoreOrder: true);
    }

    [Fact]
    public void Round_winner_starts_the_next_round_by_default()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        engine.Pass(Seat.Two);

        engine.State.RoundStarter.ShouldBe(Seat.Two);
        engine.State.ActiveSeat.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Round_loser_can_be_configured_to_start()
    {
        var scenario = new Scenario();
        scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var engine = scenario.Build(rules: MatchRules.Classic with { NextRoundStarter = NextRoundStarter.RoundLoser });

        engine.Pass(Seat.One);
        engine.Pass(Seat.Two);

        engine.State.RoundStarter.ShouldBe(Seat.One);
    }

    [Fact]
    public void Tied_round_costs_both_players_a_gem_and_alternates_the_starter()
    {
        var scenario = new Scenario();
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        engine.Pass(Seat.Two);

        engine.State.RoundResults.Single().Winner.ShouldBeNull();
        scenario.One.Gems.ShouldBe(1);
        scenario.Two.Gems.ShouldBe(1);
        engine.State.RoundStarter.ShouldBe(Seat.Two);
    }

    [Fact]
    public void Match_ends_when_a_player_runs_out_of_gems()
    {
        var scenario = new Scenario();
        scenario.Two.Gems = 1;
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        var events = engine.Pass(Seat.Two);

        engine.IsFinished.ShouldBeTrue();
        engine.State.Outcome.ShouldBe(new MatchOutcome(Seat.One));
        events.OfType<MatchEnded>().ShouldHaveSingleItem();
        engine.GetLegalCommands(Seat.One).ShouldBeEmpty();
        engine.GetLegalCommands(Seat.Two).ShouldBeEmpty();
    }

    [Fact]
    public void Tie_in_the_last_round_is_a_draw()
    {
        var scenario = new Scenario();
        scenario.One.Gems = 1;
        scenario.Two.Gems = 1;
        var engine = scenario.Build();

        engine.Pass(Seat.One);
        engine.Pass(Seat.Two);

        engine.State.Outcome.ShouldNotBeNull().IsDraw.ShouldBeTrue();
    }

    [Fact]
    public void Players_without_cards_auto_pass_through_to_the_end_of_the_match()
    {
        var scenario = new Scenario(withFillers: false);
        scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var unit = scenario.AddToHand(Seat.One, TestCards.Unit(1));
        var engine = scenario.Build();

        engine.Play(unit);

        // One plays its last card, then both sides auto-pass: One wins round 1; round 2 is an empty tie.
        engine.IsFinished.ShouldBeTrue();
        engine.State.RoundResults.Count.ShouldBe(2);
        engine.State.Outcome.ShouldBe(new MatchOutcome(Seat.One));
    }

    [Fact]
    public void Clone_is_independent_of_the_original()
    {
        var scenario = new Scenario();
        var card = scenario.AddToHand(Seat.One, TestCards.Unit(5));
        var engine = scenario.Build();

        var clone = engine.Clone();
        clone.Play(card);

        engine.State.Player(Seat.One).Hand.ShouldContain(card);
        engine.State.ActiveSeat.ShouldBe(Seat.One);
        engine.Score(Seat.One).ShouldBe(0);
        clone.Score(Seat.One).ShouldBe(5);
    }

    [Fact]
    public void FindCard_locates_cards_in_every_zone()
    {
        var scenario = new Scenario();
        var hand = scenario.AddToHand(Seat.One, TestCards.Unit(1));
        var deck = scenario.AddToDeck(Seat.Two, TestCards.Unit(2));
        var board = scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(3));
        var weather = scenario.AddWeather(TestCards.Weather(RowType.Melee));

        foreach (var card in new[] { hand, deck, board, weather })
        {
            scenario.State.FindCard(card.Id).ShouldBe(card);
        }

        scenario.State.FindCard(9999).ShouldBeNull();
    }
}
