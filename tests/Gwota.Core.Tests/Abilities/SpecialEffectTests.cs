namespace Gwota.Core.Tests.Abilities;

public class SpecialEffectTests
{
    private readonly Scenario _scenario = new();

    [Fact]
    public void Horn_occupies_the_row_slot_and_doubles_it()
    {
        var horn = _scenario.AddToHand(Seat.One, TestCards.Horn());
        _scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Unit(4, RowType.Ranged));
        var engine = _scenario.Build();

        engine.Play(horn, RowType.Ranged);

        _scenario.One.Row(RowType.Ranged).HornCard.ShouldBe(horn);
        engine.RowScore(Seat.One, RowType.Ranged).ShouldBe(8);
    }

    [Fact]
    public void Horn_cannot_target_a_row_whose_slot_is_filled()
    {
        var horn = _scenario.AddToHand(Seat.One, TestCards.Horn());
        _scenario.SetHorn(Seat.One, RowType.Melee);
        _scenario.One.Row(RowType.Siege).HasLeaderHorn = true;
        var engine = _scenario.Build();

        engine.GetLegalCommands(Seat.One).OfType<PlayCardCommand>().Where(c => c.CardId == horn.Id)
            .Select(c => c.Row).ShouldBe([RowType.Ranged]);
    }

    [Fact]
    public void Horn_with_rows_is_restricted_to_them()
    {
        var horn = _scenario.AddToHand(Seat.One, TestCards.Horn(RowType.Siege));
        var engine = _scenario.Build();

        engine.GetLegalCommands(Seat.One).OfType<PlayCardCommand>().Where(c => c.CardId == horn.Id)
            .Select(c => c.Row).ShouldBe([RowType.Siege]);
    }

    [Fact]
    public void Scorch_destroys_the_strongest_units_on_both_sides()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Scorch());
        var mine = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(7));
        var theirs = _scenario.AddToBoard(Seat.Two, RowType.Siege, TestCards.Unit(7, RowType.Siege));
        var survivor = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(6));
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(15));
        var engine = _scenario.Build();

        var events = engine.Play(scorch);

        _scenario.One.BoardCards.ShouldBeEmpty();
        _scenario.Two.BoardCards.Count().ShouldBe(2);
        _scenario.Two.Row(RowType.Melee).Cards.ShouldContain(survivor);
        _scenario.One.Discard.ShouldBe([mine, scorch], ignoreOrder: true);
        _scenario.Two.Discard.ShouldBe([theirs]);
        events.OfType<CardsDestroyed>().ShouldHaveSingleItem().Cards.Count.ShouldBe(2);
    }

    [Fact]
    public void Scorch_uses_effective_strength()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Scorch());
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(10));
        var horned = _scenario.AddToBoard(Seat.Two, RowType.Siege, TestCards.Unit(6, RowType.Siege));
        _scenario.SetHorn(Seat.Two, RowType.Siege);
        var engine = _scenario.Build();

        engine.Play(scorch);

        _scenario.Two.Discard.ShouldBe([horned]);
    }

    [Fact]
    public void Scorch_with_only_heroes_or_zero_strength_does_nothing()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Scorch());
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Hero(10));
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(0));
        var engine = _scenario.Build();

        engine.Play(scorch);

        _scenario.Two.BoardCards.Count().ShouldBe(2);
        _scenario.One.Discard.ShouldBe([scorch]);
    }

    [Fact]
    public void Destroyed_spy_goes_to_its_owner_discard()
    {
        var scorch = _scenario.AddToHand(Seat.One, TestCards.Scorch());
        var spy = _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Spy(9), owner: Seat.One);
        var engine = _scenario.Build();

        engine.Play(scorch);

        _scenario.One.Discard.ShouldContain(spy);
    }

    [Fact]
    public void Decoy_swaps_with_a_unit_which_returns_to_hand()
    {
        var decoy = _scenario.AddToHand(Seat.One, TestCards.Decoy());
        var target = _scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Unit(5, RowType.Ranged));
        var engine = _scenario.Build();

        engine.Play(decoy, RowType.Ranged, target.Id);

        _scenario.One.Row(RowType.Ranged).Cards.ShouldBe([decoy]);
        _scenario.One.Hand.ShouldContain(target);
        engine.Score(Seat.One).ShouldBe(0);
    }

    [Fact]
    public void Decoy_cannot_target_heroes_specials_or_enemy_units()
    {
        var decoy = _scenario.AddToHand(Seat.One, TestCards.Decoy());
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(10));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Decoy());
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(5));
        var engine = _scenario.Build();

        engine.GetLegalCommands(Seat.One).OfType<PlayCardCommand>().ShouldNotContain(c => c.CardId == decoy.Id);
    }

    [Fact]
    public void Decoyed_enemy_spy_becomes_yours()
    {
        var decoy = _scenario.AddToHand(Seat.One, TestCards.Decoy());
        var spy = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Spy(), owner: Seat.Two);
        var engine = _scenario.Build();

        engine.Play(decoy, RowType.Melee, spy.Id);

        var returned = _scenario.One.Hand.Where(c => c.Id == spy.Id).ShouldHaveSingleItem();
        returned.Owner.ShouldBe(Seat.One);
    }

    [Fact]
    public void Weather_affects_both_sides_of_its_rows()
    {
        var frost = _scenario.AddToHand(Seat.One, TestCards.Weather(RowType.Melee));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(8));
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(6));
        _scenario.AddToBoard(Seat.Two, RowType.Siege, TestCards.Unit(6, RowType.Siege));
        var engine = _scenario.Build();

        engine.Play(frost);

        engine.State.Weather.ShouldBe([frost]);
        engine.Score(Seat.One).ShouldBe(1);
        engine.Score(Seat.Two).ShouldBe(7);
    }

    [Fact]
    public void Multi_row_weather_affects_each_listed_row()
    {
        var storm = _scenario.AddToHand(Seat.One, TestCards.Weather(RowType.Ranged, RowType.Siege));
        _scenario.AddToBoard(Seat.Two, RowType.Ranged, TestCards.Unit(5, RowType.Ranged));
        _scenario.AddToBoard(Seat.Two, RowType.Siege, TestCards.Unit(5, RowType.Siege));
        var engine = _scenario.Build();

        engine.Play(storm);

        engine.Score(Seat.Two).ShouldBe(2);
    }

    [Fact]
    public void Clear_weather_discards_all_weather_to_owners()
    {
        var clear = _scenario.AddToHand(Seat.One, TestCards.ClearWeather());
        var mine = _scenario.AddWeather(TestCards.Weather(RowType.Melee), Seat.One);
        var theirs = _scenario.AddWeather(TestCards.Weather(RowType.Siege), Seat.Two);
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(8));
        var engine = _scenario.Build();

        var events = engine.Play(clear);

        engine.State.Weather.ShouldBeEmpty();
        engine.Score(Seat.One).ShouldBe(8);
        _scenario.One.Discard.ShouldBe([mine, clear], ignoreOrder: true);
        _scenario.Two.Discard.ShouldBe([theirs]);
        events.OfType<WeatherCleared>().ShouldHaveSingleItem();
    }
}
