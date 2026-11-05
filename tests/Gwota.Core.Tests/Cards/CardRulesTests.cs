namespace Gwota.Core.Tests.Cards;

public class CardRulesTests
{
    [Fact]
    public void Non_hero_units_exclude_heroes_and_specials()
    {
        TestCards.Unit(3).IsNonHeroUnit.ShouldBeTrue();
        TestCards.Hero(10).IsNonHeroUnit.ShouldBeFalse();
        TestCards.Decoy().IsNonHeroUnit.ShouldBeFalse();
    }

    [Theory]
    [InlineData(Seat.One, Seat.Two)]
    [InlineData(Seat.Two, Seat.One)]
    public void Spies_are_placed_on_the_opponent_side(Seat player, Seat expected)
    {
        TestCards.Spy().PlacementSide(player).ShouldBe(expected);
        TestCards.Unit(3).PlacementSide(player).ShouldBe(player);
    }

    [Theory]
    [InlineData(LeaderAbility.HornRow, true)]
    [InlineData(LeaderAbility.ScorchRow, true)]
    [InlineData(LeaderAbility.PlayWeatherFromDeck, true)]
    [InlineData(LeaderAbility.ClearWeather, false)]
    [InlineData(LeaderAbility.Scorch, false)]
    [InlineData(LeaderAbility.ReturnFromDiscard, false)]
    [InlineData(LeaderAbility.ReturnFromBoard, false)]
    public void Only_row_abilities_use_a_row(LeaderAbility ability, bool expected) => ability.UsesRow().ShouldBe(expected);
}
