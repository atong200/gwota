namespace Gwota.Core.Tests.Abilities;

public class StrengthCalculatorTests
{
    private readonly Scenario _scenario = new();

    private int StrengthOf(CardInstance card, Seat side = Seat.One, RowType row = RowType.Melee) =>
        StrengthCalculator.Calculate(_scenario.State, _scenario.State.Player(side).Row(row), card);

    [Fact]
    public void Plain_unit_has_its_base_strength()
    {
        var card = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(6));

        StrengthOf(card).ShouldBe(6);
    }

    [Fact]
    public void Special_cards_on_the_board_have_no_strength()
    {
        var decoy = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Decoy());

        StrengthOf(decoy).ShouldBe(0);
    }

    [Fact]
    public void Weather_reduces_non_hero_units_in_affected_rows_to_one()
    {
        var melee = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(6));
        var ranged = _scenario.AddToBoard(Seat.One, RowType.Ranged, TestCards.Unit(6, RowType.Ranged));
        var zero = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(0));
        _scenario.AddWeather(TestCards.Weather(RowType.Melee));

        StrengthOf(melee).ShouldBe(1);
        StrengthOf(zero).ShouldBe(0);
        StrengthOf(ranged, row: RowType.Ranged).ShouldBe(6);
    }

    [Fact]
    public void Heroes_ignore_weather_horn_and_morale()
    {
        var hero = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Hero(10));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.WithAbility(1, UnitAbility.MoraleBoost));
        _scenario.AddWeather(TestCards.Weather(RowType.Melee));
        _scenario.SetHorn(Seat.One, RowType.Melee);

        StrengthOf(hero).ShouldBe(10);
    }

    [Fact]
    public void Tight_bond_multiplies_by_copies_in_the_same_row()
    {
        var bond = TestCards.WithAbility(4, UnitAbility.TightBond);
        var first = _scenario.AddToBoard(Seat.One, RowType.Melee, bond);
        _scenario.AddToBoard(Seat.One, RowType.Melee, bond);
        _scenario.AddToBoard(Seat.One, RowType.Melee, bond);
        _scenario.AddToBoard(Seat.One, RowType.Ranged, bond);

        StrengthOf(first).ShouldBe(12);
    }

    [Fact]
    public void Morale_boost_adds_one_to_every_other_unit_in_the_row()
    {
        var unit = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var booster = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.WithAbility(2, UnitAbility.MoraleBoost));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.WithAbility(2, UnitAbility.MoraleBoost, id: "booster_b"));

        StrengthOf(unit).ShouldBe(7);
        StrengthOf(booster).ShouldBe(3);
    }

    [Fact]
    public void Horn_card_doubles_the_row()
    {
        var unit = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        _scenario.SetHorn(Seat.One, RowType.Melee);

        StrengthOf(unit).ShouldBe(10);
    }

    [Fact]
    public void Horn_unit_doubles_others_but_not_itself()
    {
        var unit = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        var horn = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.WithAbility(2, UnitAbility.Horn));

        StrengthOf(unit).ShouldBe(10);
        StrengthOf(horn).ShouldBe(2);
    }

    [Fact]
    public void Horn_does_not_stack()
    {
        var unit = _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.WithAbility(2, UnitAbility.Horn));
        _scenario.SetHorn(Seat.One, RowType.Melee);

        StrengthOf(unit).ShouldBe(10);
    }

    [Fact]
    public void Leader_horn_doubles_the_row()
    {
        var unit = _scenario.AddToBoard(Seat.One, RowType.Siege, TestCards.Unit(4, RowType.Siege));
        _scenario.One.Row(RowType.Siege).HasLeaderHorn = true;

        StrengthOf(unit, row: RowType.Siege).ShouldBe(8);
    }

    [Fact]
    public void Modifiers_apply_in_order_weather_bond_morale_horn()
    {
        var bond = TestCards.WithAbility(8, UnitAbility.TightBond);
        var first = _scenario.AddToBoard(Seat.One, RowType.Melee, bond);
        _scenario.AddToBoard(Seat.One, RowType.Melee, bond);
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.WithAbility(3, UnitAbility.MoraleBoost));
        _scenario.AddWeather(TestCards.Weather(RowType.Melee));
        _scenario.SetHorn(Seat.One, RowType.Melee);

        // weather: 8 → 1, bond ×2 → 2, morale +1 → 3, horn ×2 → 6
        StrengthOf(first).ShouldBe(6);
    }

    [Fact]
    public void Totals_sum_rows_and_sides_independently()
    {
        _scenario.AddToBoard(Seat.One, RowType.Melee, TestCards.Unit(5));
        _scenario.AddToBoard(Seat.One, RowType.Siege, TestCards.Unit(3, RowType.Siege));
        _scenario.AddToBoard(Seat.Two, RowType.Melee, TestCards.Unit(9));

        StrengthCalculator.RowTotal(_scenario.State, _scenario.One.Row(RowType.Melee)).ShouldBe(5);
        StrengthCalculator.PlayerTotal(_scenario.State, Seat.One).ShouldBe(8);
        StrengthCalculator.PlayerTotal(_scenario.State, Seat.Two).ShouldBe(9);
    }
}
